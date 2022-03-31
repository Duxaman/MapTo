using MapTo.Extensions;
using System;
using static MapTo.Sources.Constants;

namespace MapTo.Sources
{
    internal static class MapClassSource
    {
        internal static SourceCode Generate(MappingModel model)
        {
            using var builder = new SourceBuilder()
                .WriteLine(GeneratedFilesHeader)
                .WriteNullableContextOptionIf(model.Options.SupportNullableReferenceTypes)
                .WriteUsings(model.Usings)
                .WriteLine()

                // Namespace declaration
                .WriteLine($"namespace {model.Namespace}")
                .WriteOpeningBracket()

                .GenerateSourceTypeExtensionClass(model)

                // End namespace declaration
                .WriteClosingBracket();

            return new(builder.ToString(), $"{model.Namespace}.{model.SourceTypeIdentifierName}.{model.TypeIdentifierName}.g.cs");
        }


        private static SourceBuilder GenerateConvertorMethodsXmlDocs(this SourceBuilder builder, MappingModel model, string sourceClassParameterName)
        {
            if (!model.Options.GenerateXmlDocument)
            {
                return builder;
            }

            return builder
                .WriteLine("/// <summary>")
                .WriteLine($"/// Creates a new instance of <see cref=\"{model.TypeIdentifierName}\"/> and sets its participating properties")
                .WriteLine($"/// using the property values from <paramref name=\"{sourceClassParameterName}\"/>.")
                .WriteLine("/// </summary>")
                .WriteLine($"/// <param name=\"{sourceClassParameterName}\">The instance of <see cref=\"{model.SourceType}\"/> to use as source.</param>")
                .WriteLine($"/// <returns>A new instance of <see cred=\"{model.TypeIdentifierName}\"/> -or- <c>null</c> if <paramref name=\"{sourceClassParameterName}\"/> is <c>null</c>.</returns>");
        }

        private static SourceBuilder GenerateSourceTypeExtensionClass(this SourceBuilder builder, MappingModel model)
        {
            return builder
                .WriteLine($"{model.Options.GeneratedMethodsAccessModifier.ToLowercaseString()} static class {model.SourceTypeIdentifierName}To{model.TypeIdentifierName}Extensions")
                .WriteOpeningBracket()
                .GenerateSourceTypeExtensionMethod(model)
                .GenerateCreateMethod(model)
                .WriteClosingBracket();
        }

        private static string ExtractShortTypeName(string LongTypeName)
        {
            var TypeNameArr = LongTypeName.Split('.');
            return $"{TypeNameArr[TypeNameArr.Length - 1]}";
        }


        public static SourceBuilder GenerateCreateMethod(this SourceBuilder builder, MappingModel model)
        {
            var sourceClassParameterName = model.SourceTypeIdentifierName.ToCamelCase();
            const string mappingContextParameterName = "context";

            builder
                .WriteLineIf(model.Options.SupportNullableStaticAnalysis, $"[return: NotNullIfNotNull(\"{sourceClassParameterName}\")]")
                .WriteLine($"{model.Options.GeneratedMethodsAccessModifier.ToLowercaseString()} static {model.TypeIdentifierName}{model.Options.NullableReferenceSyntax} Create{model.TypeIdentifierName}({model.SourceType}{model.Options.NullableReferenceSyntax} {sourceClassParameterName}, MappingContext{model.Options.NullableReferenceSyntax} context=null)")
                .WriteOpeningBracket()
                .WriteLine($"if ({mappingContextParameterName} == null) {mappingContextParameterName} = new {MappingContextSource.ClassName}();")
                .WriteLine($"if ({sourceClassParameterName} == null) throw new ArgumentNullException(nameof({sourceClassParameterName}));")
                .WriteLine()
                .WriteLine($"var Mapped = new {model.TypeIdentifierName}();")
                .WriteLine()
                .WriteLine($"{mappingContextParameterName}.{MappingContextSource.RegisterMethodName}({sourceClassParameterName}, Mapped);");

            foreach (var property in model.MappedProperties)
            {
                if (property.TypeConverter is null)
                {
                    var SourceTypeName = property.MappedSourcePropertyTypeName?.Split('.');
                    var ShortSourceTypeName = $"{SourceTypeName?[SourceTypeName.Length - 1]}";
                    if (property.IsEnumerable)
                    {
                        var ShortDestTypeName = ExtractShortTypeName(property.EnumerableTypeArgument!);
                        builder.WriteLine($"Mapped.{property.Name} = {sourceClassParameterName}.{property.SourcePropertyName}.Select(pr => {mappingContextParameterName}.{MappingContextSource.MapMethodName}<{property.MappedSourcePropertyTypeName}, {property.EnumerableTypeArgument}>(pr, {ShortSourceTypeName}To{ShortDestTypeName}Extensions.Create{ShortDestTypeName})).ToList();");
                    }
                    else
                    {
                        if (property.MappedSourcePropertyTypeName is null)
                        {
                            builder.WriteLine($"Mapped.{property.Name} = {sourceClassParameterName}.{property.SourcePropertyName};");
                        }
                        else
                        {
                            var ShortDestTypeName = ExtractShortTypeName(property.Type);
                            builder.WriteLine($"Mapped.{property.Name} = {mappingContextParameterName}.{MappingContextSource.MapMethodName}<{property.MappedSourcePropertyTypeName}, {property.Type}>({sourceClassParameterName}.{property.SourcePropertyName}, {ShortSourceTypeName}To{ShortDestTypeName}Extensions.Create{ShortDestTypeName});");
                        }
                    }
                }
                else
                {
                    var parameters = property.TypeConverterParameters.IsEmpty
                        ? "null"
                        : $"new object[] {{ {string.Join(", ", property.TypeConverterParameters)} }}";

                    builder.WriteLine($"Mapped.{property.Name} = new {property.TypeConverter}().Convert({sourceClassParameterName}.{property.SourcePropertyName}, {parameters});");
                }
            }

            builder.WriteLine("return Mapped;");

            return builder.WriteClosingBracket();
        }


        private static SourceBuilder GenerateSourceTypeExtensionMethod(this SourceBuilder builder, MappingModel model)
        {
            var sourceClassParameterName = model.SourceTypeIdentifierName.ToCamelCase();
            builder
              .GenerateConvertorMethodsXmlDocs(model, sourceClassParameterName)
              .WriteLineIf(model.Options.SupportNullableStaticAnalysis, $"[return: NotNullIfNotNull(\"{sourceClassParameterName}\")]")
              .WriteLine($"{model.Options.GeneratedMethodsAccessModifier.ToLowercaseString()} static {model.TypeIdentifierName}{model.Options.NullableReferenceSyntax} To{model.TypeIdentifierName}(this {model.SourceType}{model.Options.NullableReferenceSyntax} {sourceClassParameterName}, MappingContext{model.Options.NullableReferenceSyntax} context=null)")
              .WriteOpeningBracket()
              .WriteLine($"return Create{model.TypeIdentifierName}({sourceClassParameterName}, context);");
            return builder.WriteClosingBracket();
        }
    }
}