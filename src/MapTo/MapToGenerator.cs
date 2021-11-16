using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MapTo.Extensions;
using MapTo.Sources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MapTo
{
    /// <summary>
    /// MapTo source generator.
    /// </summary>
    [Generator]
    public class MapToGenerator : ISourceGenerator
    {
        /// <inheritdoc />
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new MapToSyntaxReceiver());
        }

        /// <inheritdoc />
        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                Debugger.Launch();
                var options = SourceGenerationOptions.From(context);
                var compilation = context.Compilation
                    .AddSource(ref context, MapFromAttributeSource.Generate(options))
                    .AddSource(ref context, MapToAttributeSource.Generate(options))
                    .AddSource(ref context, IgnorePropertyAttributeSource.Generate(options))
                    .AddSource(ref context, ITypeConverterSource.Generate(options))
                    .AddSource(ref context, MapTypeConverterAttributeSource.Generate(options))
                    .AddSource(ref context, MapPropertyAttributeSource.Generate(options))
                    .AddSource(ref context, MappingContextSource.Generate(options));

                if (context.SyntaxReceiver is MapToSyntaxReceiver receiver && receiver.CandidateTypes.Any())
                {
                    AddGeneratedMappingsClasses(context, compilation, receiver.CandidateTypes, options);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }


        }

        private static void AddGeneratedMappingsClasses(GeneratorExecutionContext context, Compilation compilation, IEnumerable<TypeDeclarationSyntax> candidateTypes, SourceGenerationOptions options)
        {
            foreach (var typeDeclarationSyntax in candidateTypes)
            {
                var type = compilation.GetTypeBySyntax(typeDeclarationSyntax);
                var sourceTypes = GetSourceTypes(compilation, typeDeclarationSyntax);
                var targetTypes = GetSourceTypes(compilation, typeDeclarationSyntax);

                var contexts = new List<MappingContext>();

                //Adding MapFrom contexts
                contexts.AddRange(sourceTypes.Select(s => MappingContext.Create(compilation, options, s, type)));
                //Adding MapTo contexts
                contexts.AddRange(targetTypes.Select(t => MappingContext.Create(compilation, options, type, t)));

                foreach (var mappingContext in contexts)
                {
                    mappingContext.Diagnostics.ForEach(context.ReportDiagnostic);

                    if (mappingContext.Model is null)
                    {
                        continue;
                    }

                    var (source, hintName) = typeDeclarationSyntax switch
                    {
                        ClassDeclarationSyntax => MapClassSource.Generate(mappingContext.Model),
                        RecordDeclarationSyntax => MapRecordSource.Generate(mappingContext.Model),
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    context.AddSource(hintName, source);
                }

            }
        }

        private static IReadOnlyList<INamedTypeSymbol> GetSourceTypes(Compilation compilation, TypeDeclarationSyntax typeDeclarationSyntax)
        {
            var semanticModel = compilation.GetSemanticModel(typeDeclarationSyntax.SyntaxTree);
            return GetTypeSymbolFromAttribute(compilation, typeDeclarationSyntax.GetAttributes(MapFromAttributeSource.AttributeName), semanticModel);
        }

        private static IReadOnlyList<INamedTypeSymbol> GetTargetTypes(Compilation compilation, TypeDeclarationSyntax typeDeclarationSyntax)
        {
            var semanticModel = compilation.GetSemanticModel(typeDeclarationSyntax.SyntaxTree);
            return GetTypeSymbolFromAttribute(compilation, typeDeclarationSyntax.GetAttributes(MapToAttributeSource.AttributeName), semanticModel);
        }

        private static IReadOnlyList<INamedTypeSymbol> GetTypeSymbolFromAttribute(Compilation compilation, IEnumerable<SyntaxNode> attributeSyntaxList, SemanticModel? semanticModel = null)
        {
            var result = new List<INamedTypeSymbol>();
            if (attributeSyntaxList is null || attributeSyntaxList.IsEmpty())
            {
                return result;
            }

            foreach (var attributeSyntax in attributeSyntaxList)
            {
                semanticModel ??= compilation.GetSemanticModel(attributeSyntax.SyntaxTree);
                var sourceTypeExpressionSyntax = attributeSyntax
                    .DescendantNodes()
                    .OfType<TypeOfExpressionSyntax>()
                    .SingleOrDefault();

                if (sourceTypeExpressionSyntax is not null && semanticModel.GetTypeInfo(sourceTypeExpressionSyntax.Type).Type is INamedTypeSymbol typeSymbol)
                {
                    result.Add(typeSymbol);
                }
            }

            return result;
        }
    }
}