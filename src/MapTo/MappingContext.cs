using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using MapTo.Extensions;
using MapTo.Sources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MapTo
{
    internal abstract class MappingContext
    {
        private readonly List<SymbolDisplayPart> _ignoredNamespaces;

        protected MappingContext(Compilation compilation, SourceGenerationOptions sourceGenerationOptions, INamedTypeSymbol sourceType, INamedTypeSymbol targetType)
        {
            _ignoredNamespaces = new();
            Diagnostics = ImmutableArray<Diagnostic>.Empty;
            Usings = ImmutableArray.Create("System", Constants.RootNamespace);
            SourceGenerationOptions = sourceGenerationOptions;
            SourceType = sourceType;
            TargetType = targetType;
            Compilation = compilation;

            IgnorePropertyAttributeTypeSymbol = compilation.GetTypeByMetadataNameOrThrow(IgnorePropertyAttributeSource.FullyQualifiedName);
            MapTypeConverterAttributeTypeSymbol = compilation.GetTypeByMetadataNameOrThrow(MapTypeConverterAttributeSource.FullyQualifiedName);
            TypeConverterInterfaceTypeSymbol = compilation.GetTypeByMetadataNameOrThrow(ITypeConverterSource.FullyQualifiedName);
            MapPropertyAttributeTypeSymbol = compilation.GetTypeByMetadataNameOrThrow(MapPropertyAttributeSource.FullyQualifiedName);
            MapFromAttributeTypeSymbol = compilation.GetTypeByMetadataNameOrThrow(MapFromAttributeSource.FullyQualifiedName);
            MapToAttributeTypeSymbol = compilation.GetTypeByMetadataNameOrThrow(MapToAttributeSource.FullyQualifiedName);
            MappingContextTypeSymbol = compilation.GetTypeByMetadataNameOrThrow(MappingContextSource.FullyQualifiedName);

            AddUsingIfRequired(sourceGenerationOptions.SupportNullableStaticAnalysis, "System.Diagnostics.CodeAnalysis");
        }

        public ImmutableArray<Diagnostic> Diagnostics { get; private set; }

        public MappingModel? Model { get; private set; }

        protected Compilation Compilation { get; }

        protected INamedTypeSymbol IgnorePropertyAttributeTypeSymbol { get; }

        protected INamedTypeSymbol MapFromAttributeTypeSymbol { get; }

        protected INamedTypeSymbol MapToAttributeTypeSymbol { get; }

        protected INamedTypeSymbol MappingContextTypeSymbol { get; }

        protected INamedTypeSymbol MapPropertyAttributeTypeSymbol { get; }

        protected INamedTypeSymbol MapTypeConverterAttributeTypeSymbol { get; }

        protected SourceGenerationOptions SourceGenerationOptions { get; }

        protected INamedTypeSymbol TypeConverterInterfaceTypeSymbol { get; }

        protected INamedTypeSymbol SourceType { get; }

        protected INamedTypeSymbol TargetType { get; }

        protected ImmutableArray<string> Usings { get; private set; }

        public static MappingContext Create(Compilation compilation, SourceGenerationOptions sourceGenerationOptions, INamedTypeSymbol sourceType, INamedTypeSymbol targetType)
        {
            MappingContext context = (sourceType.TypeKind, sourceType.IsRecord) switch
            {
                (TypeKind.Class, false)  => new ClassMappingContext(compilation, sourceGenerationOptions, sourceType, targetType),
                (_, true) => new RecordMappingContext(compilation, sourceGenerationOptions, sourceType, targetType),
                _ => throw new ArgumentOutOfRangeException()
            };

            context.Model = context.CreateMappingModel(sourceType, targetType);

            return context;
        }

        protected void AddDiagnostic(Diagnostic diagnostic)
        {
            Diagnostics = Diagnostics.Add(diagnostic);
        }

        protected void AddUsingIfRequired(ISymbol? namedTypeSymbol) =>
            AddUsingIfRequired(namedTypeSymbol?.ContainingNamespace.IsGlobalNamespace == false, namedTypeSymbol?.ContainingNamespace);

        protected void AddUsingIfRequired(bool condition, INamespaceSymbol? ns) =>
            AddUsingIfRequired(condition && ns is not null && !_ignoredNamespaces.Contains(ns.ToDisplayParts().First()), ns?.ToDisplayString());

        protected void AddUsingIfRequired(bool condition, string? ns)
        {
            //NameSpace + 
            if (ns is not null && condition && ns != TargetType.ContainingNamespace.ToDisplayString() && !Usings.Contains(ns))
            {
                Usings = Usings.Add(ns);
            }
        }

        protected IPropertySymbol? FindSourceProperty(ISymbol sourceTypeSymbol, IEnumerable<IPropertySymbol> sourceProperties, ISymbol property)
        {

            var propertyName = property
                .GetAttributes(MapPropertyAttributeTypeSymbol)
                .FirstOrDefault(a =>
                {
                    if (a.GetAttributeParameterValue(MapPropertyAttributeSource.SourceTypeName) is string propertyTypeFromAttribute)
                    {
                        return propertyTypeFromAttribute == sourceTypeSymbol.ToDisplayString();
                    }
                    return true;
                }
                )
                ?.NamedArguments
                .SingleOrDefault(a => a.Key == MapPropertyAttributeSource.SourcePropertyNamePropertyName)
                .Value.Value as string ?? property.Name;

            return sourceProperties.SingleOrDefault(p => p.Name == propertyName);
        }

        protected abstract ImmutableArray<MappedProperty> GetMappedProperties(ITypeSymbol typeSymbol, ITypeSymbol sourceTypeSymbol, bool isInheritFromMappedBaseClass);


        protected bool IsTypeInheritFromMappedBaseClass()
        { //Get base types?
            var baseTypes = TargetType.GetBaseTypes();
            return baseTypes is not null && baseTypes
                .Any(t => (t?.GetAttribute(MapFromAttributeTypeSymbol) != null) || (t?.GetAttribute(MapToAttributeTypeSymbol) != null));
        }

        protected virtual MappedProperty? MapProperty(ISymbol sourceTypeSymbol, IReadOnlyCollection<IPropertySymbol> sourceProperties, ISymbol property)
        {
            var sourceProperty = FindSourceProperty(sourceTypeSymbol, sourceProperties, property);
            if (sourceProperty is null || !property.TryGetTypeSymbol(out var propertyType))
            {
                return null;
            }

            string? converterFullyQualifiedName = null;
            var converterParameters = ImmutableArray<string>.Empty;
            ITypeSymbol? mappedSourcePropertyType = null;
            ITypeSymbol? enumerableTypeArgumentType = null;

            if (!Compilation.HasCompatibleTypes(sourceProperty, property))
            {
                if (!TryGetMapTypeConverter(sourceTypeSymbol, property, sourceProperty, out converterFullyQualifiedName, out converterParameters) &&
                    !TryGetNestedObjectMappings(property, out mappedSourcePropertyType, out enumerableTypeArgumentType))
                {
                    return null;
                }
            }

            AddUsingIfRequired(propertyType);
            AddUsingIfRequired(enumerableTypeArgumentType);
            AddUsingIfRequired(mappedSourcePropertyType);

            return new MappedProperty(
                property.Name,
                ToQualifiedDisplayName(propertyType) ?? propertyType.Name,
                converterFullyQualifiedName,
                converterParameters.ToImmutableArray(),
                sourceProperty.Name,
                ToQualifiedDisplayName(mappedSourcePropertyType),
                ToQualifiedDisplayName(enumerableTypeArgumentType));
        }

        protected bool TryGetMapTypeConverter(ISymbol sourceTypeSymbol, ISymbol property, IPropertySymbol sourceProperty, out string? converterFullyQualifiedName,
            out ImmutableArray<string> converterParameters)
        {
            converterFullyQualifiedName = null;
            converterParameters = ImmutableArray<string>.Empty;

            if (!Diagnostics.IsEmpty())
            {
                return false;
            }

            var typeConverterAttribute = property
                .GetAttributes(MapTypeConverterAttributeTypeSymbol)
                .SingleOrDefault(a => {
                    if (a.GetAttributeParameterValue(MapTypeConverterAttributeSource.SourceTypeName) is ISymbol propertyTypeFromAttribute)
                    {
                        return propertyTypeFromAttribute.Equals(sourceTypeSymbol, SymbolEqualityComparer.Default);
                    }

                    return true;
                });

            if (typeConverterAttribute?.ConstructorArguments.First().Value is not INamedTypeSymbol converterTypeSymbol)
            {
                return false;
            }

            var baseInterface = GetTypeConverterBaseInterface(converterTypeSymbol, property, sourceProperty);
            if (baseInterface is null)
            {
                AddDiagnostic(DiagnosticsFactory.InvalidTypeConverterGenericTypesError(property, sourceProperty));
                return false;
            }

            converterFullyQualifiedName = converterTypeSymbol.ToDisplayString();
            converterParameters = GetTypeConverterParameters(typeConverterAttribute);
            return true;
        }

        protected bool TryGetNestedObjectMappings(ISymbol property, out ITypeSymbol? mappedSourcePropertyType, out ITypeSymbol? enumerableTypeArgument)
        {
            mappedSourcePropertyType = null;
            enumerableTypeArgument = null;

            if (!Diagnostics.IsEmpty())
            {
                return false;
            }

            if (!property.TryGetTypeSymbol(out var propertyType))
            {
                AddDiagnostic(DiagnosticsFactory.NoMatchingPropertyTypeFoundError(property));
                return false;
            }

            var mapFromAttribute = propertyType.GetAttribute(MapFromAttributeTypeSymbol);
            if (mapFromAttribute is null &&
                propertyType is INamedTypeSymbol namedTypeSymbol &&
                !propertyType.IsPrimitiveType() &&
                (Compilation.IsGenericEnumerable(propertyType) || propertyType.AllInterfaces.Any(i => Compilation.IsGenericEnumerable(i))))
            {
                enumerableTypeArgument = namedTypeSymbol.TypeArguments.First();
                mapFromAttribute = enumerableTypeArgument.GetAttribute(MapFromAttributeTypeSymbol);
            }

            mappedSourcePropertyType = mapFromAttribute?.ConstructorArguments.First().Value as INamedTypeSymbol;

            if (mappedSourcePropertyType is null && enumerableTypeArgument is null)
            {
                AddDiagnostic(DiagnosticsFactory.NoMatchingPropertyTypeFoundError(property));
            }

            return Diagnostics.IsEmpty();
        }

        private static ImmutableArray<string> GetTypeConverterParameters(AttributeData typeConverterAttribute)
        {
            var converterParameter = typeConverterAttribute.ConstructorArguments.Skip(1).FirstOrDefault();
            return converterParameter.IsNull
                ? ImmutableArray<string>.Empty
                : converterParameter.Values.Where(v => v.Value is not null).Select(v => v.Value!.ToSourceCodeString()).ToImmutableArray();
        }

        private MappingModel? CreateMappingModel()
        {
            _ignoredNamespaces.Add(SourceType.ContainingNamespace.ToDisplayParts().First());

            var typeIdentifierName = TargetType.Name;
            var sourceTypeIdentifierName = SourceType.Name;
            var isTypeInheritFromMappedBaseClass = IsTypeInheritFromMappedBaseClass();
            var shouldGenerateSecondaryConstructor = ShouldGenerateSecondaryConstructor(SourceType);

            var mappedProperties = GetMappedProperties(TargetType, SourceType, isTypeInheritFromMappedBaseClass);
            if (!mappedProperties.Any())
            {
                //todo: check if correct
                AddDiagnostic(DiagnosticsFactory.NoMatchingPropertyFoundError(TargetType.Locations.First(), typeSymbol, sourceTypeSymbol));
                return null;
            }

            AddUsingIfRequired(mappedProperties.Any(p => p.IsEnumerable), "System.Linq");

            return new MappingModel(
                SourceGenerationOptions,
                TargetType.ContainingNamespace.ToDisplayString(),
                typeIdentifierName,
                sourceTypeSymbol.ContainingNamespace.ToDisplayString(),
                sourceTypeIdentifierName,
                sourceTypeSymbol.ToDisplayString(),
                mappedProperties,
                isTypeInheritFromMappedBaseClass,
                Usings,
                shouldGenerateSecondaryConstructor);
        }
        private INamedTypeSymbol? GetTypeConverterBaseInterface(ITypeSymbol converterTypeSymbol, ISymbol property, IPropertySymbol sourceProperty)
        {
            if (!property.TryGetTypeSymbol(out var propertyType))
            {
                return null;
            }

            return converterTypeSymbol.AllInterfaces
                .SingleOrDefault(i =>
                    i.TypeArguments.Length == 2 &&
                    SymbolEqualityComparer.Default.Equals(i.ConstructedFrom, TypeConverterInterfaceTypeSymbol) &&
                    SymbolEqualityComparer.Default.Equals(sourceProperty.Type, i.TypeArguments[0]) &&
                    SymbolEqualityComparer.Default.Equals(propertyType, i.TypeArguments[1]));
        }

        private bool ShouldGenerateSecondaryConstructor()
        {
            var constructor = TargetType.Constructors.SingleOrDefault(c => c.Parameters.Count() == 1 && SymbolEqualityComparer.Default.Equals(c.Parameters.Single().Type, SourceType));
            //var constructorSyntax = SourceType.DescendantNodes()
            //    .OfType<ConstructorDeclarationSyntax>()
            //    .SingleOrDefault(c =>
            //        c.ParameterList.Parameters.Count == 1 &&
            //        SymbolEqualityComparer.Default.Equals(semanticModel.GetTypeInfo(c.ParameterList.Parameters.Single().Type!).ConvertedType, sourceTypeSymbol));

            if (constructor is null)
            {
                // Secondary constructor is not defined.
                return true;
            }

            if (constructor.Parameters is not { Length: 2 } arguments ||
                !SymbolEqualityComparer.Default.Equals(arguments[0].Type, MappingContextTypeSymbol) ||
                !SymbolEqualityComparer.Default.Equals(arguments[1].Type, SourceType))
            {
                //todo: Проверить корректность
                AddDiagnostic(DiagnosticsFactory.MissingConstructorArgument(constructor));
            }

            return false;
        }

        private string? ToQualifiedDisplayName(ISymbol? symbol)
        {
            if (symbol is null)
            {
                return null;
            }

            var containingNamespace = TargetType.ContainingNamespace.ToDisplayString();
            var symbolNamespace = symbol.ContainingNamespace.ToDisplayString();
            return  containingNamespace != symbolNamespace && _ignoredNamespaces.Contains(symbol.ContainingNamespace.ToDisplayParts().First())
                ? symbol.ToDisplayString()
                : symbol.Name;
        }
    }
}
