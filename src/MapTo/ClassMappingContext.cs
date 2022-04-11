using System.Collections.Immutable;
using System.Linq;
using MapTo.Extensions;
using MapTo.Sources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MapTo
{
    internal class ClassMappingContext : MappingContext
    {
        internal ClassMappingContext(Compilation compilation, SourceGenerationOptions sourceGenerationOptions)
            : base(compilation, sourceGenerationOptions) { }

        /// <inheritdoc />
        protected override ImmutableArray<MappedProperty> GetMappedProperties(ITypeSymbol argumentClassTypeSymbol, ITypeSymbol currentClassTypeSymbol, TypeDeclarationSyntax currentClassTypeSyntax,
            TypeDeclarationSyntax argumentClassTypeSyntax, MappingDirection mappingDirection = MappingDirection.From)
        {
            /*
             * just filter all needed properties 
             */

            if (mappingDirection == MappingDirection.From)
            {
                //currentClass is destination class

                var argumentClassPropertySymbols = argumentClassTypeSymbol
                    .GetAllMembers()
                    .OfType<IPropertySymbol>()
                    .ToArray();

                return currentClassTypeSymbol
                    .GetAllMembers()
                    .OfType<IPropertySymbol>()
                    .Where(p => !p.HasAttributeForType(IgnorePropertyAttributeTypeSymbol, argumentClassTypeSymbol, IgnorePropertyAttributeSource.TargetTypeName)) //filter ignore attributes from destination class
                    .Select(currentClassProperty => MapProperty(argumentClassTypeSymbol, currentClassTypeSymbol ,argumentClassPropertySymbols, currentClassProperty, argumentClassTypeSyntax, false))
                    .ToList()
                    .Where(mappedProperty => mappedProperty is not null)
                    .ToImmutableArray()!;
            }
            else
            {
                //argumentClass is destination class 
                var currentClassTypeSymbols = currentClassTypeSymbol
                    .GetAllMembers()
                    .OfType<IPropertySymbol>()
                    .Where(p => !p.HasAttributeForType(IgnorePropertyAttributeTypeSymbol, argumentClassTypeSymbol, IgnorePropertyAttributeSource.TargetTypeName)) //filter ignore attributes from source class
                    .ToArray();

                return argumentClassTypeSymbol
                    .GetAllMembers()
                    .OfType<IPropertySymbol>()
                    .Select(argumentClassProperty => MapProperty(currentClassTypeSymbol, argumentClassTypeSymbol, currentClassTypeSymbols, argumentClassProperty, currentClassTypeSyntax, true))
                    .ToList()
                    .Where(mappedProperty => mappedProperty is not null)
                    .ToImmutableArray()!;
            }
        }
    }
}