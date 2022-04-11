using static MapTo.Sources.Constants;

namespace MapTo.Sources
{
    internal static class MapAttributeSource
    {
        internal const string AttributeName = "Map";
        internal const string AttributeClassName = AttributeName + "Attribute";
        internal const string FullyQualifiedName = RootNamespace + "." + AttributeClassName;
        internal const string AttributeTargetType = "TargetType";
        internal const string AttributeDirection = "Direction";
        
        internal static SourceCode Generate(SourceGenerationOptions options)
        {
            using var builder = new SourceBuilder()
                .WriteLine(GeneratedFilesHeader)
                .WriteLine("using System;")
                .WriteLine()
                .WriteLine($"namespace {RootNamespace}")
                .WriteOpeningBracket();

            if (options.GenerateXmlDocument)
            {
                builder
                    .WriteLine("/// <summary>")
                    .WriteLine("/// Specifies that the annotated class can be mapped from/to the provided <see cref=\"targetType\"/>.")
                    .WriteLine("/// </summary>");
            }

            builder
                .WriteLine("[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]")
                .WriteLine($"public sealed class {AttributeName}Attribute : Attribute")
                .WriteOpeningBracket();

            if (options.GenerateXmlDocument)
            {
                builder
                    .WriteLine("/// <summary>")
                    .WriteLine($"/// Initializes a new instance of the <see cref=\"{AttributeName}Attribute\"/> class with the specified <paramref name=\"targetType\"/>.")
                    .WriteLine("/// </summary>")
                    .WriteLine("/// <param name=\"targetType\">The type of to map from.</param>")
                    .WriteLine("/// <param name=\"direction\">The direction of mapping process</param>");

            }

            builder
                .WriteLine($"public {AttributeName}Attribute(Type targetType, MappingDirection direction = MappingDirection.From)")
                .WriteOpeningBracket()
                .WriteLine("Direction = direction;")
                .WriteLine("TargetType = targetType;")
                .WriteClosingBracket()
                .WriteLine();

            if (options.GenerateXmlDocument)
            {
                builder
                    .WriteLine("/// <summary>")
                    .WriteLine("/// Gets the target type of mapping")
                    .WriteLine("/// </summary>");
            }

            builder
                .WriteLine("public Type TargetType { get; }")
                .WriteLine();


            if (options.GenerateXmlDocument)
            {
                builder
                    .WriteLine("/// <summary>")
                    .WriteLine("/// Gets the direction of mapping")
                    .WriteLine("/// </summary>");
            }

            builder
                .WriteLine("public MappingDirection Direction { get; }")


                .WriteClosingBracket() // class

                .WriteLine()

                .WriteLine("public enum MappingDirection")
                .WriteOpeningBracket()
                .WriteLine("From,")
                .WriteLine("To")
                .WriteClosingBracket()

                .WriteClosingBracket(); // namespace

            return new(builder.ToString(), $"{AttributeName}Attribute.g.cs");
        }
    }
}