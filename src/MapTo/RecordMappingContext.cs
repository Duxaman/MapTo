using System.Collections.Immutable;
using System.Linq;
using MapTo.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MapTo
{
    internal class RecordMappingContext : MappingContext
    {
        internal RecordMappingContext(Compilation compilation, SourceGenerationOptions sourceGenerationOptions, INamedTypeSymbol sourceType, INamedTypeSymbol targetType)
            : base(compilation, sourceGenerationOptions, sourceType, targetType) { }

        protected override ImmutableArray<MappedProperty> GetMappedProperties(bool isInheritFromMappedBaseClass)
        {
            var sourceProperties = SourceType.GetAllMembers().OfType<IPropertySymbol>().ToArray();
            return TargetType.GetMembers()
                .OfType<IMethodSymbol>()
                .OrderByDescending(s => s.Parameters.Length)
                .First(s => s.Name == ".ctor")
                .Parameters
                .Where(p => !p.HasAttribute(IgnorePropertyAttributeTypeSymbol))
                .Select(property => MapProperty(sourceProperties, property))
                .Where(mappedProperty => mappedProperty is not null)
                .ToImmutableArray()!;
        }
    }
}