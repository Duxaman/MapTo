using System;
using System.Collections.Immutable;
using MapTo.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MapTo
{
    internal enum MappingDirection
    {
        From,
        To
    }

    internal enum AccessModifier
    {
        Public,
        Internal,
        Private
    }

    internal enum NullStaticAnalysisState
    {
        Default,
        Enabled,
        Disabled
    }

    internal record SourceCode(string Text, string HintName);

    /// <summary>
    /// Mapped property
    /// Destination fields are related to the class
    /// that need to be generated
    /// </summary>
    internal record MappedProperty(
        string DestinationPropertyName,
        string DestinationPropertyType,
        string? TypeConverter,
        ImmutableArray<string> TypeConverterParameters,
        string SourcePropertyName,
        string? MappedSourcePropertyTypeName,
        string? DestinationPropertyEnumerableTypeArgument)
    {
        public bool IsEnumerable => DestinationPropertyEnumerableTypeArgument is not null;
    }

    /// <summary>
    /// Creates MappingModel where
    /// Destination fields are related to the class
    /// that need to be generated
    /// </summary>
    internal record MappingModel (
        SourceGenerationOptions Options,
        string? Namespace,
        SyntaxTokenList Modifiers,
        string DestinationType,
        string DestinationTypeIdentifierName,
        string SourceNamespace,
        string SourceTypeIdentifierName,
        string SourceTypeFullName,
        ImmutableArray<MappedProperty> MappedProperties,
        ImmutableArray<string> Usings
    )
    {
        public string SourceType => SourceTypeFullName;
    }

    internal record SourceGenerationOptions(
        AccessModifier ConstructorAccessModifier,
        AccessModifier GeneratedMethodsAccessModifier,
        bool GenerateXmlDocument,
        bool SupportNullableReferenceTypes,
        bool SupportNullableStaticAnalysis)
    {
        internal static SourceGenerationOptions From(GeneratorExecutionContext context)
        {
            const string allowNullAttributeName = "System.Diagnostics.CodeAnalysis.AllowNullAttribute";
            var supportNullableStaticAnalysis = context.GetBuildGlobalOption(propertyName: nameof(SupportNullableStaticAnalysis), NullStaticAnalysisState.Default);
            var supportNullableReferenceTypes = context.Compilation.Options.NullableContextOptions is NullableContextOptions.Warnings or NullableContextOptions.Enable;

            return new(
                ConstructorAccessModifier: context.GetBuildGlobalOption(propertyName: nameof(ConstructorAccessModifier), AccessModifier.Public),
                GeneratedMethodsAccessModifier: context.GetBuildGlobalOption(propertyName: nameof(GeneratedMethodsAccessModifier), AccessModifier.Public),
                GenerateXmlDocument: context.GetBuildGlobalOption(propertyName: nameof(GenerateXmlDocument), true),
                SupportNullableReferenceTypes: supportNullableReferenceTypes,
                SupportNullableStaticAnalysis: supportNullableStaticAnalysis switch
                {
                    NullStaticAnalysisState.Enabled => true,
                    NullStaticAnalysisState.Disabled => false,
                    _ => context.Compilation is CSharpCompilation { LanguageVersion: >= LanguageVersion.CSharp8 } cs && cs.TypeByMetadataNameExists(allowNullAttributeName)
                }
            );
        }

        public string NullableReferenceSyntax => SupportNullableReferenceTypes ? "?" : string.Empty;
    }
}