using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MapTo.Extensions;
using MapTo.Sources;
using MapTo;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MapTo
{
    internal abstract class MappingContext
    {
        private readonly List<SymbolDisplayPart> _ignoredNamespaces;

        public static MappingContext Create(Compilation compilation, SourceGenerationOptions sourceGenerationOptions, TypeDeclarationSyntax typeSyntax)
        {
            MappingContext context = typeSyntax switch
            {
                ClassDeclarationSyntax => new ClassMappingContext(compilation, sourceGenerationOptions),
                RecordDeclarationSyntax => throw new NotImplementedException(),
                _ => throw new ArgumentOutOfRangeException()
            };

            context.Models = context.CreateMappingModelList(typeSyntax);

            return context;
        }

        protected MappingContext(Compilation compilation, SourceGenerationOptions sourceGenerationOptions)
        {
            _ignoredNamespaces = new();
            Diagnostics = ImmutableArray<Diagnostic>.Empty;
            Usings = ImmutableArray.Create("System", Constants.RootNamespace);
            SourceGenerationOptions = sourceGenerationOptions;
            Compilation = compilation;

            IgnorePropertyAttributeTypeSymbol = compilation.GetTypeByMetadataNameOrThrow(IgnorePropertyAttributeSource.FullyQualifiedName);
            MapTypeConverterAttributeTypeSymbol = compilation.GetTypeByMetadataNameOrThrow(MapTypeConverterAttributeSource.FullyQualifiedName);
            TypeConverterInterfaceTypeSymbol = compilation.GetTypeByMetadataNameOrThrow(ITypeConverterSource.FullyQualifiedName);
            MapPropertyAttributeTypeSymbol = compilation.GetTypeByMetadataNameOrThrow(MapPropertyAttributeSource.FullyQualifiedName);
            MapAttributeTypeSymbol = compilation.GetTypeByMetadataNameOrThrow(MapAttributeSource.FullyQualifiedName);
            MappingContextTypeSymbol = compilation.GetTypeByMetadataNameOrThrow(MappingContextSource.FullyQualifiedName);

            AddUsingIfRequired(sourceGenerationOptions.SupportNullableStaticAnalysis, "System.Diagnostics.CodeAnalysis");
        }

        public ImmutableArray<Diagnostic> Diagnostics { get; private set; }

        public IEnumerable<MappingModel> Models { get; private set; } = new List<MappingModel>();

        #region PROTECTED PROPERTIES
        protected Compilation Compilation { get; }

        protected INamedTypeSymbol IgnorePropertyAttributeTypeSymbol { get; }

        protected INamedTypeSymbol MapAttributeTypeSymbol { get; }

        protected INamedTypeSymbol MappingContextTypeSymbol { get; }

        protected INamedTypeSymbol MapPropertyAttributeTypeSymbol { get; }

        protected INamedTypeSymbol MapTypeConverterAttributeTypeSymbol { get; }

        protected SourceGenerationOptions SourceGenerationOptions { get; }

        protected INamedTypeSymbol TypeConverterInterfaceTypeSymbol { get; }

        protected ImmutableArray<string> Usings { get; private set; }

        #endregion

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
            if (ns is not null && condition && !Usings.Contains(ns))
            {
                Usings = Usings.Add(ns);
            }
        }

        /// <summary>
        /// Returns an immutable array of source and destination class mapped properties
        /// </summary>
        /// <param name="argumentClassTypeSymbol">TypeSymbol of the class that stated as argument of MapAttribute</param>
        /// <param name="currentClassTypeSymbol">TypeSymbol of the class where MapAttribute is stated</param>
        /// <param name="currentClassTypeSyntax">TypeDeclarationSyntax of the class where MapAttribute is stated</param>
        /// <param name="argumentClassTypeSyntax">TypeDeclarationSyntax of the class that stated as argument of MapAttribute</param>
        /// <param name="mappingDirection">Specifies in which direction mapping process will be applied (FROM: argumentClass -> currentClass TO: currentClass-> argumentClass)</param>
        /// <returns></returns>
        protected abstract ImmutableArray<MappedProperty> GetMappedProperties(ITypeSymbol argumentClassTypeSymbol, ITypeSymbol currentClassTypeSymbol, TypeDeclarationSyntax currentClassTypeSyntax,
            TypeDeclarationSyntax argumentClassTypeSyntax, MappingDirection mappingDirection = MappingDirection.From);

        protected IReadOnlyList<(INamedTypeSymbol, MappingDirection)> GetTargetTypesSymbol(TypeDeclarationSyntax typeDeclarationSyntax, SemanticModel semanticModel)
        {
            return GetTargetTypesSymbol(typeDeclarationSyntax.GetAttributes(MapAttributeSource.AttributeClassName, semanticModel));
        }

        protected IReadOnlyList<(INamedTypeSymbol, MappingDirection)> GetTargetTypesSymbol(IEnumerable<AttributeData> attributeData)
        {
            var result = new List<(INamedTypeSymbol, MappingDirection)>();
            if (attributeData is null || attributeData.IsEmpty())
            {
                return result;
            }

            foreach (var attribute in attributeData)
            {
                var res = attribute.ConstructorArguments;

                result.Add(((INamedTypeSymbol)res.First(a => a.Kind != TypedConstantKind.Enum).Value!,
                            (MappingDirection)res.First(a => a.Kind == TypedConstantKind.Enum).Value!));
            }

            return result;
        }

        protected bool IsTypeInheritFromMappedBaseClass(SemanticModel semanticModel, TypeDeclarationSyntax typeSyntax)
        {
            return typeSyntax.BaseList is not null && typeSyntax.BaseList.Types
                .Select(t => semanticModel.GetTypeInfo(t.Type).Type)
                .Any(t => t?.GetAttribute(MapAttributeTypeSymbol) != null);
        }

        private ISymbol? FindSourceProperty(ISymbol sourceTypeSymbol, ISymbol destinationTypeSymbol, IReadOnlyCollection<IPropertySymbol> sourceProperties,
            ISymbol destinationProperty, bool useSourceAttributes)
        {
            string? propertyName;
            if (!useSourceAttributes)
            {
                /*
                    Determine whether current destination property has MapPropertyAttribute or not
                    If it has such attribute, then argument of that attribute is the name of property
                    that destination property need to be mapped. 

                    If no attribute found => try to map by standard name
                     */
                propertyName = destinationProperty
                   .GetAttributes(MapPropertyAttributeTypeSymbol)
                   .SingleOrDefault(a =>
                   {
                       if (a.GetAttributeParameterValue(MapPropertyAttributeSource.TargetTypeName) is ISymbol typeInAttribute)
                       {
                           return typeInAttribute.Equals(sourceTypeSymbol, SymbolEqualityComparer.Default);
                       }

                       return true;
                   }
                   )
                   ?.NamedArguments
                   .SingleOrDefault(a => a.Key == MapPropertyAttributeSource.TargetPropertyNamePropertyName)
                   .Value.Value as string ?? destinationProperty.Name;
            }
            else
            {
                /*
                 * among all source properties that has not got ignore attribute try to find a property that has mapattribute
                 * with propertyname argument = destination property
                 * if there's no such use destination property name
                 */
                propertyName = sourceProperties.Select(pr =>
                {
                    if ((pr.GetAttributes(MapPropertyAttributeTypeSymbol) //has mapproperty attribute
                    .SingleOrDefault(a =>
                    {
                        if (a.GetAttributeParameterValue(MapPropertyAttributeSource.TargetTypeName) is ISymbol typeInAttribute) //that points to destination type
                        {
                            return typeInAttribute.Equals(destinationTypeSymbol, SymbolEqualityComparer.Default);
                        }
                        return true; //if no argument then it applies to all types
                    }
                    )?.NamedArguments
                    .SingleOrDefault(a => a.Key == MapPropertyAttributeSource.TargetPropertyNamePropertyName)
                    .Value.Value as string == destinationProperty.Name)
                    )
                    {
                        return pr;
                    }
                    return null;
                })
                .FirstOrDefault(el => el is not null)?.Name ?? destinationProperty.Name;
            }
            return sourceProperties.SingleOrDefault(p => p.Name == propertyName);
        }

        /// <summary>
        /// Maps source and destionation property
        /// </summary>
        /// <param name="sourceTypeSymbol">source class typesymbol</param>
        /// <param name="sourceProperties">source class properties</param>
        /// <param name="destinationPropertySymbol">destionation property to map to</param>
        /// <param name="destinationTypeSyntax">source class typesyntax</param>
        /// <returns></returns>
        protected virtual MappedProperty? MapProperty(ISymbol sourceTypeSymbol, ISymbol destinationTypeSymbol, IReadOnlyCollection<IPropertySymbol> sourceProperties,
            ISymbol destinationPropertySymbol, TypeDeclarationSyntax destinationTypeSyntax, bool useSourceAttributes)
        {
            ISymbol? sourcePropertySymbol = FindSourceProperty(sourceTypeSymbol, destinationTypeSymbol, sourceProperties, destinationPropertySymbol, useSourceAttributes);
            if (sourcePropertySymbol is null || !destinationPropertySymbol.TryGetTypeSymbol(out var destinationPropertyTypeSymbol))
            {
                return null;
            }

            string? converterFullyQualifiedName = null;
            var converterParameters = ImmutableArray<string>.Empty;
            ITypeSymbol? mappedTargetPropertyType = null;
            ITypeSymbol? enumerableTypeArgumentType = null;

            //If types can't be converted
            if (!Compilation.HasCompatibleTypes(sourcePropertySymbol, destinationPropertySymbol))
            {
                //try find converter
                if (!TryGetMapTypeConverter(sourceTypeSymbol, destinationTypeSymbol, destinationPropertySymbol, sourcePropertySymbol as IPropertySymbol, useSourceAttributes, out converterFullyQualifiedName,
                    out converterParameters))
                {
                    //if there's no converter, check if those type can be mapped
                    if (!TryGetNestedObjectMappings(sourcePropertySymbol, destinationPropertySymbol, out mappedTargetPropertyType, out enumerableTypeArgumentType))
                    {
                        AddDiagnostic(DiagnosticsFactory.NoMatchingPropertyTypeFoundError(sourcePropertySymbol));
                        return null;
                    }
                }

            }

            AddUsingIfRequired(destinationPropertyTypeSymbol);
            AddUsingIfRequired(enumerableTypeArgumentType);
            AddUsingIfRequired(mappedTargetPropertyType);

            return new MappedProperty(
                destinationPropertySymbol.Name,
                ToQualifiedDisplayName(destinationPropertyTypeSymbol, destinationTypeSyntax) ?? destinationPropertyTypeSymbol.Name,
                converterFullyQualifiedName,
                converterParameters.ToImmutableArray(),
                sourcePropertySymbol.Name,
                ToQualifiedDisplayName(mappedTargetPropertyType, destinationTypeSyntax),
                ToQualifiedDisplayName(enumerableTypeArgumentType, destinationTypeSyntax));
        }

        /// <summary>
        /// Tries to get appropriate type converter to the specified property pair
        /// </summary>
        /// <param name="sourceTypeSymbol"></param>
        /// <param name="destinationTypeSybol">generation class type symbol</param>
        /// <param name="destinationProperty">destination property of generation class</param>
        /// <param name="sourceProperty">source property</param>
        /// <param name="useSourceAttributes">specifies does function need to look for the converterattribute at source property or at destination property</param>
        /// <param name="converterFullyQualifiedName">fully qualified name of the found convertor</param>
        /// <param name="converterParameters">parameters of the found convertor</param>
        /// <returns></returns>
        protected bool TryGetMapTypeConverter(ISymbol sourceTypeSymbol, ISymbol destinationTypeSybol, ISymbol destinationProperty, IPropertySymbol sourceProperty, bool useSourceAttributes,
            out string? converterFullyQualifiedName, out ImmutableArray<string> converterParameters)
        {
            converterFullyQualifiedName = null;
            converterParameters = ImmutableArray<string>.Empty;

            if (!Diagnostics.IsEmpty())
            {
                return false;
            }

            AttributeData? typeConverterAttribute;

            /*
             * Try to locate typeconverter attribute at needed property.
             * Also make sure that type specified in that attribute = type at the matched property from the other side

             */
            if (useSourceAttributes)
            {
                typeConverterAttribute = sourceProperty
                .GetAttributes(MapTypeConverterAttributeTypeSymbol)
                .SingleOrDefault(a =>
                {
                    if (a.GetAttributeParameterValue(MapTypeConverterAttributeSource.TargetTypeName) is ISymbol propertyTypeFromAttribute)
                    {
                        return propertyTypeFromAttribute.Equals(destinationTypeSybol, SymbolEqualityComparer.Default);
                    }
                    return true;
                });
            }
            else
            {
                typeConverterAttribute = destinationProperty
                .GetAttributes(MapTypeConverterAttributeTypeSymbol)
                .SingleOrDefault(a =>
                {
                    if (a.GetAttributeParameterValue(MapTypeConverterAttributeSource.TargetTypeName) is ISymbol propertyTypeFromAttribute)
                    {
                        return propertyTypeFromAttribute.Equals(sourceTypeSymbol, SymbolEqualityComparer.Default);
                    }

                    return true;
                });
            }

            /*
             * Check if converter is defined, and implements converter's interface
             */

            if (typeConverterAttribute?.ConstructorArguments.First().Value is not INamedTypeSymbol converterTypeSymbol)
            {
                return false;
            }

            var baseInterface = GetTypeConverterBaseInterface(converterTypeSymbol, destinationProperty, sourceProperty);
            if (baseInterface is null)
            {
                AddDiagnostic(DiagnosticsFactory.InvalidTypeConverterGenericTypesError(destinationProperty, sourceProperty));
                return false;
            }

            converterFullyQualifiedName = converterTypeSymbol.ToDisplayString();
            converterParameters = GetTypeConverterParameters(typeConverterAttribute);
            return true;
        }
        /// <summary>
        /// Looks for a map attribute that have a direction of mappingDirection and whose target type is pointed at mappedProperty 
        /// at a property_to_check property and if that attribute is present
        /// retrieves mappedSourcePropertyType for a source type of map attribute
        /// and enumerableTypeArgument for a destination type of an attribute if destination type is enumerable
        /// </summary>
        /// <param name="propertyToCheck">ISymbol of the property which type will be used to look for an attribute</param>
        /// <param name="mappedProperty">ISymbol of the property that was mapped to propertyToCheck </param>
        /// <param name="mappingDirection">Direction of the attribute to look for at property</param>
        /// <param name="mappedSourcePropertyType">Source property type if an attribute was found</param>
        /// <param name="enumerableTypeArgument">Destination property enumerable argument, if it is enumerable</param>
        /// <returns></returns>
        protected bool TryGetPropertyObjectMappings(ISymbol propertyToCheck, ISymbol mappedProperty, MappingDirection mappingDirection, out ITypeSymbol? mappedSourcePropertyType, out ITypeSymbol? enumerableTypeArgument)
        {
            mappedSourcePropertyType = null;
            enumerableTypeArgument = null;

            if (!propertyToCheck.TryGetTypeSymbol(out var propertyType))
            {
                return false;
            }

            if (!mappedProperty.TryGetTypeSymbol(out var neededTypeInAttributeArgument))
            {
                return false;
            }

            if (neededTypeInAttributeArgument.IsTypeParameterizedEnumerable(Compilation))
            {
                neededTypeInAttributeArgument = ((INamedTypeSymbol)neededTypeInAttributeArgument).TypeArguments.First();
            }

            AttributeData? mapAttribute;

            //get mapattribute that pointed to neededTypeInAttributeArgument and has direction set to mappingDirection
            if (propertyType.IsTypeParameterizedEnumerable(Compilation))
            {
                enumerableTypeArgument = ((INamedTypeSymbol)propertyType).TypeArguments.First();
                mapAttribute = TryFindNeededMapAttribute(enumerableTypeArgument, neededTypeInAttributeArgument, mappingDirection, MapAttributeTypeSymbol);
            }
            else
            {
                mapAttribute = TryFindNeededMapAttribute(propertyType, neededTypeInAttributeArgument, mappingDirection, MapAttributeTypeSymbol);
            }

            if (mapAttribute is null)
            {
                return false;
            }
            else
            {
                if (mappingDirection == MappingDirection.From)
                {
                    mappedSourcePropertyType = neededTypeInAttributeArgument;
                }
                else
                {
                    if (enumerableTypeArgument is not null)
                    {
                        mappedSourcePropertyType = enumerableTypeArgument;
                        enumerableTypeArgument = neededTypeInAttributeArgument;
                    }
                    else
                        mappedSourcePropertyType = propertyType;
                }
            }

            return true;

            static AttributeData? TryFindNeededMapAttribute(ITypeSymbol propertyTypeSymbol, ITypeSymbol neededTypeInAttributeArgument, MappingDirection mappingDirection, INamedTypeSymbol MapAttributeTypeSymbol)
            {
                return propertyTypeSymbol.GetAttributes(MapAttributeTypeSymbol)
                .SingleOrDefault(a =>
                {
                    var res = a.ConstructorArguments;
                    var propertyTypeFromAttribute = (INamedTypeSymbol)res.First(a => a.Kind != TypedConstantKind.Enum).Value!;
                    var direction = (MappingDirection)res.First(a => a.Kind == TypedConstantKind.Enum).Value!;
                    return propertyTypeFromAttribute.Equals(neededTypeInAttributeArgument, SymbolEqualityComparer.Default) && direction == mappingDirection;
                });
            }
        }

        /// <summary>
        /// Tries to find nested target property type
        /// </summary>
        /// <param name="destinationProperty">property to seek mapped type</param>
        /// <param name="mappedSourcePropertyType">mapped property type if type is resolved</param>
        /// <param name="enumerableTypeArgument">dest property enumerable argument type if it's enumerable</param>
        /// <returns></returns>
        protected bool TryGetNestedObjectMappings(ISymbol sourceProperty, ISymbol destinationProperty, out ITypeSymbol? mappedSourcePropertyType, out ITypeSymbol? enumerableTypeArgument)
        {

            /* Get source property typesymbol,
             * Get destination property typesymbol, 
             * 
             * the latter typesymbol is the destination class, now we need to check do we have any from/to mappings to that class
             * 
             * to do so, first check does that destination property typesymbol has MAPFROM attribute that pointed to source property typeSymbol,
             * if it does get mappedSourcePropertyType and enumerableTypeArgument from here
             * otherwise approach from the other side,
             * 
             * check does source property typesymbol has MAPTO attribute that is pointed to destination property typesymbol, if it does we're good to go
             */

            if (!TryGetPropertyObjectMappings(destinationProperty, sourceProperty, MappingDirection.From, out mappedSourcePropertyType, out enumerableTypeArgument))
            {
                return TryGetPropertyObjectMappings(sourceProperty, destinationProperty, MappingDirection.To, out mappedSourcePropertyType, out enumerableTypeArgument);
            }
            return true;
        }

        private static ImmutableArray<string> GetTypeConverterParameters(AttributeData typeConverterAttribute)
        {
            var converterParameter = typeConverterAttribute.ConstructorArguments.Skip(1).FirstOrDefault();
            return converterParameter.IsNull
                ? ImmutableArray<string>.Empty
                : converterParameter.Values.Where(v => v.Value is not null).Select(v => v.Value!.ToSourceCodeString()).ToImmutableArray();
        }


        /// <summary>
        /// Creates list of mapping models for each map attribute of the class
        /// </summary>
        /// <param name="currentClassTypeSyntax">class syntax containing map attribute</param>
        /// <returns></returns>
        private IReadOnlyList<MappingModel> CreateMappingModelList(TypeDeclarationSyntax currentClassTypeSyntax)
        {
            var emptyList = new List<MappingModel>(0);

            var semanticModel = Compilation.GetSemanticModel(currentClassTypeSyntax.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(currentClassTypeSyntax) is not INamedTypeSymbol currentClassTypeSymbol)
            {
                AddDiagnostic(DiagnosticsFactory.TypeNotFoundError(currentClassTypeSyntax.GetLocation(), currentClassTypeSyntax.Identifier.ValueText));
                return emptyList;
            }

            //get all types and mapping directions from current currentClassTypeSyntax attributes if any
            IReadOnlyList<(INamedTypeSymbol, MappingDirection)> targetTypeSymbolList = GetTargetTypesSymbol(currentClassTypeSyntax, semanticModel);
            if (targetTypeSymbolList is null || !targetTypeSymbolList.Any())
            {
                AddDiagnostic(DiagnosticsFactory.MapFromAttributeNotFoundError(currentClassTypeSyntax.GetLocation()));
                return emptyList;
            }

            return targetTypeSymbolList.Select(argumentClassTypeSymbol =>
            {
                var argumentClassTypeSyntax = (TypeDeclarationSyntax)argumentClassTypeSymbol.Item1.DeclaringSyntaxReferences.First().GetSyntax()!;
                return CreateMappingModel(argumentClassTypeSymbol.Item1, currentClassTypeSymbol, currentClassTypeSyntax, argumentClassTypeSyntax, argumentClassTypeSymbol.Item2);
            }).ToList().FindAll(el => el is not null)!;

        }

        /// <summary>
        /// Creates mapping model based on input values
        /// </summary>
        /// <param name="argumentClassTypeSymbol">INamedTypeSymbol of the class that stated as argument of MapAttribute</param>
        /// <param name="currentClassTypeSymbol">INamedTypeSymbol of the class where MapAttribute is stated</param>
        /// <param name="currentClassTypeSyntax">TypeDeclarationSyntax of the class where MapAttribute is stated</param>
        /// <param name="argumentClassTypeSyntax">TypeDeclarationSyntax of the class that stated as argument of MapAttribute</param>
        /// <param name="mappingDirection">Specifies in which direction mapping process will be applied (FROM: argumentClass -> currentClass TO: currentClass-> argumentClass)</param>
        /// <returns></returns>
        private MappingModel? CreateMappingModel(INamedTypeSymbol argumentClassTypeSymbol, INamedTypeSymbol currentClassTypeSymbol,
            TypeDeclarationSyntax currentClassTypeSyntax, TypeDeclarationSyntax argumentClassTypeSyntax,
            MappingDirection mappingDirection = MappingDirection.From)
        {
            ImmutableArray<MappedProperty> mappedProperties;

            //_ignoredNamespaces.Add(sourceTypeSymbol.ContainingNamespace.ToDisplayParts().First());


            mappedProperties = GetMappedProperties(argumentClassTypeSymbol, currentClassTypeSymbol, currentClassTypeSyntax,
                                                   argumentClassTypeSyntax, mappingDirection);
            if (!mappedProperties.Any())
            {
                //AddDiagnostic(DiagnosticsFactory.NoMatchingPropertyFoundError(destinationTypeSyntax.GetLocation(), destTypeSymbol, sourceTypeSymbol));
                return null;
            }

            AddUsingIfRequired(mappedProperties.Any(p => p.IsEnumerable), "System.Linq");

            if (mappingDirection == MappingDirection.From)
            {
                //currentClass is destination class


                AddUsingIfRequired(true, currentClassTypeSyntax.GetNamespace());

                return new MappingModel(
                SourceGenerationOptions,
                "MapTo.CreateMethodExtensions",
                currentClassTypeSyntax.Modifiers,
                currentClassTypeSyntax.Keyword.Text,
                currentClassTypeSyntax.GetIdentifierName(),
                argumentClassTypeSymbol.ContainingNamespace.ToDisplayString(),
                argumentClassTypeSyntax.GetIdentifierName(),
                argumentClassTypeSymbol.ToDisplayString(),
                mappedProperties,
                Usings);
            }
            else
            {
                //argumentClass is destination class

                AddUsingIfRequired(true, argumentClassTypeSyntax.GetNamespace());

                return new MappingModel(
                SourceGenerationOptions,
                "MapTo.CreateMethodExtensions",
                argumentClassTypeSyntax.Modifiers,
                argumentClassTypeSyntax.Keyword.Text,
                argumentClassTypeSyntax.GetIdentifierName(),
                currentClassTypeSymbol.ContainingNamespace.ToDisplayString(),
                currentClassTypeSyntax.GetIdentifierName(),
                currentClassTypeSymbol.ToDisplayString(),
                mappedProperties,
                Usings);
            }
        }

        private INamedTypeSymbol? GetTypeConverterBaseInterface(ITypeSymbol converterTypeSymbol, ISymbol destinationProperty, IPropertySymbol sourceProperty)
        {
            if (!destinationProperty.TryGetTypeSymbol(out var propertyType))
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

        private string? ToQualifiedDisplayName(ISymbol? symbol, TypeDeclarationSyntax typeSyntax)
        {
            if (symbol is null)
            {
                return null;
            }

            var containingNamespace = typeSyntax.GetNamespace();
            var symbolNamespace = symbol.ContainingNamespace.ToDisplayString();
            return containingNamespace != symbolNamespace && _ignoredNamespaces.Contains(symbol.ContainingNamespace.ToDisplayParts().First())
                ? symbol.ToDisplayString()
                : symbol.Name;
        }
    }
}