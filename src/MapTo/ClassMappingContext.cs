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
        internal ClassMappingContext(Compilation compilation, SourceGenerationOptions sourceGenerationOptions, INamedTypeSymbol sourceType, INamedTypeSymbol targetType)
            : base(compilation, sourceGenerationOptions, sourceType, targetType) { }

        protected override ImmutableArray<MappedProperty> GetMappedProperties(bool isInheritFromMappedBaseClass)
        {
            var sourceProperties = SourceType.GetAllMembers().OfType<IPropertySymbol>().ToArray();

            return TargetType
                .GetAllMembers(!isInheritFromMappedBaseClass)
                .OfType<IPropertySymbol>()
                .Where(p => !p.HasAttributeForType(IgnorePropertyAttributeTypeSymbol, SourceType, IgnorePropertyAttributeSource.SourceTypeName))
                .Select(property => MapProperty(sourceProperties, property))
                .Where(mappedProperty => mappedProperty is not null)
                .ToImmutableArray()!;
        }
    }
}