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

            return new(builder.ToString(), $"{model.Namespace}.{model.SourceTypeIdentifierName}.{model.DestinationTypeIdentifierName}.g.cs");
        }


        private static SourceBuilder GenerateConvertorMethodsXmlDocs(this SourceBuilder builder, MappingModel model, string sourceClassParameterName)
        {
            if (!model.Options.GenerateXmlDocument)
            {
                return builder;
            }

            return builder
                .WriteLine("/// <summary>")
                .WriteLine($"/// Creates a new instance of <see cref=\"{model.DestinationTypeIdentifierName}\"/> and sets its participating properties")
                .WriteLine($"/// using the property values from <paramref name=\"{sourceClassParameterName}\"/>.")
                .WriteLine("/// </summary>")
                .WriteLine($"/// <param name=\"{sourceClassParameterName}\">The instance of <see cref=\"{model.SourceType}\"/> to use as source.</param>")
                .WriteLine($"/// <returns>A new instance of <see cred=\"{model.DestinationTypeIdentifierName}\"/> -or- <c>null</c> if <paramref name=\"{sourceClassParameterName}\"/> is <c>null</c>.</returns>");
        }

        private static SourceBuilder GenerateSourceTypeExtensionClass(this SourceBuilder builder, MappingModel model)
        {
            return builder
                .WriteLine($"{model.Options.GeneratedMethodsAccessModifier.ToLowercaseString()} static class {model.SourceTypeIdentifierName}To{model.DestinationTypeIdentifierName}Extensions")
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
                .WriteLine($"{model.Options.GeneratedMethodsAccessModifier.ToLowercaseString()} static {model.DestinationTypeIdentifierName}{model.Options.NullableReferenceSyntax} Create{model.DestinationTypeIdentifierName}({model.SourceType}{model.Options.NullableReferenceSyntax} {sourceClassParameterName}, MappingContext{model.Options.NullableReferenceSyntax} context=null)")
                .WriteOpeningBracket()
                .WriteLine($"if ({mappingContextParameterName} == null) {mappingContextParameterName} = new {MappingContextSource.ClassName}();")
                .WriteLine($"if ({sourceClassParameterName} == null) throw new ArgumentNullException(nameof({sourceClassParameterName}));")
                .WriteLine()
                .WriteLine($"var Mapped = new {model.DestinationTypeIdentifierName}();")
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
                        var ShortDestTypeName = ExtractShortTypeName(property.DestinationPropertyEnumerableTypeArgument!);
                        builder.WriteLine($"Mapped.{property.DestinationPropertyName} = {sourceClassParameterName}.{property.SourcePropertyName}.Select(pr => {mappingContextParameterName}.{MappingContextSource.MapMethodName}<{property.MappedSourcePropertyTypeName}, {property.DestinationPropertyEnumerableTypeArgument}>(pr, {ShortSourceTypeName}To{ShortDestTypeName}Extensions.Create{ShortDestTypeName})).ToList();");
                    }
                    else
                    {
                        if (property.MappedSourcePropertyTypeName is null)
                        {
                            builder.WriteLine($"Mapped.{property.DestinationPropertyName} = {sourceClassParameterName}.{property.SourcePropertyName};");
                        }
                        else
                        {
                            var ShortDestTypeName = ExtractShortTypeName(property.DestinationPropertyType);
                            builder.WriteLine($"Mapped.{property.DestinationPropertyName} = {mappingContextParameterName}.{MappingContextSource.MapMethodName}<{property.MappedSourcePropertyTypeName}, {property.DestinationPropertyType}>({sourceClassParameterName}.{property.SourcePropertyName}, {ShortSourceTypeName}To{ShortDestTypeName}Extensions.Create{ShortDestTypeName});");
                        }
                    }
                }
                else
                {
                    var parameters = property.TypeConverterParameters.IsEmpty
                        ? "null"
                        : $"new object[] {{ {string.Join(", ", property.TypeConverterParameters)} }}";

                    builder.WriteLine($"Mapped.{property.DestinationPropertyName} = new {property.TypeConverter}().Convert({sourceClassParameterName}.{property.SourcePropertyName}, {parameters});");
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
              .WriteLine($"{model.Options.GeneratedMethodsAccessModifier.ToLowercaseString()} static {model.DestinationTypeIdentifierName}{model.Options.NullableReferenceSyntax} To{model.DestinationTypeIdentifierName}(this {model.SourceType}{model.Options.NullableReferenceSyntax} {sourceClassParameterName})")
              .WriteOpeningBracket()
              .WriteLine($"return Create{model.DestinationTypeIdentifierName}({sourceClassParameterName});");
            return builder.WriteClosingBracket();
        }
    }
}