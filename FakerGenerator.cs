using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace FakerGeneratorCLI
{
    internal static class FakerGenerator
    {
        private static readonly StringBuilder primitiveFakerStringBuilder = new();
        private static readonly StringBuilder extendedFakerStringBuilder = new();
        private static readonly StringBuilder extensionsFakerStringBuilder = new();

        public static string GenerateFakerCode(string modelFilePath, string outputNamespace)
        {
            try
            {
                primitiveFakerStringBuilder.Clear();
                extendedFakerStringBuilder.Clear();
                extensionsFakerStringBuilder.Clear();

                string modelCode = File.ReadAllText(modelFilePath);
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(modelCode);
                var root = syntaxTree.GetCompilationUnitRoot();
                var classDeclaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

                if (classDeclaration != null)
                {
                    string modelClassName = classDeclaration.Identifier.Text;
                    string modelNamespace = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "GeneratedNamespace";
                    var properties = classDeclaration.Members.OfType<PropertyDeclarationSyntax>();

                    string fakerClassName = $"{modelClassName}Faker";
                    return ApplyTemplate(outputNamespace, modelClassName, modelNamespace, properties, fakerClassName);
                }
                else
                {
                    return "// Error: Could not find class in the model file.";
                }
            }
            catch (FileNotFoundException)
            {
                return $"// Error: Model file not found at '{modelFilePath}'.";
            }
            catch (Exception ex)
            {
                return $"// An error occurred: {ex.Message}";
            }
        }

        private static string ApplyTemplate(string outputNamespace, string modelClassName, string modelNamespace, IEnumerable<PropertyDeclarationSyntax> properties, string fakerClassName)
        {
            primitiveFakerStringBuilder.AppendLine($@"using System.Collections.Generic;");
            primitiveFakerStringBuilder.AppendLine($@"using Bogus;");
            primitiveFakerStringBuilder.AppendLine($@"using {modelNamespace};");
            primitiveFakerStringBuilder.AppendLine();
            primitiveFakerStringBuilder.AppendLine($@"namespace {outputNamespace}");
            primitiveFakerStringBuilder.AppendLine($@"{{");
            primitiveFakerStringBuilder.AppendLine($@"    /// <summary>");
            primitiveFakerStringBuilder.AppendLine($@"    /// Faker class for <see cref=""{modelClassName}""/>");
            primitiveFakerStringBuilder.AppendLine($@"    /// </summary>");
            primitiveFakerStringBuilder.AppendLine($@"    public static class {fakerClassName}");
            primitiveFakerStringBuilder.AppendLine($@"    {{");
            primitiveFakerStringBuilder.AppendLine($@"        /// <summary>");
            primitiveFakerStringBuilder.AppendLine($@"        /// Primitive definition of <see cref=""{modelClassName}""/>");
            primitiveFakerStringBuilder.AppendLine($@"        /// It is safe to use to seed properties of other classes fakers");
            primitiveFakerStringBuilder.AppendLine($@"        /// </summary>");
            primitiveFakerStringBuilder.AppendLine($@"        public static Faker<{modelClassName}> Primitive()");
            primitiveFakerStringBuilder.AppendLine($@"        {{");
            primitiveFakerStringBuilder.AppendLine($@"            return new Faker<{modelClassName}>()");

            extendedFakerStringBuilder.AppendLine();
            extendedFakerStringBuilder.AppendLine($@"        /// <summary>");
            extendedFakerStringBuilder.AppendLine($@"        /// Extended definition of <see cref=""{modelClassName}""/>");
            extendedFakerStringBuilder.AppendLine($@"        /// This should not be used by other fakers to avoid circular dependencies");
            extendedFakerStringBuilder.AppendLine($@"        /// </summary>");
            extendedFakerStringBuilder.AppendLine($@"        public static Faker<{modelClassName}> Extended()");
            extendedFakerStringBuilder.AppendLine($@"        {{");
            extendedFakerStringBuilder.AppendLine($@"            return Primitive()");

            foreach (var property in properties)
            {
                string propertyName = property.Identifier.Text;
                string propertyType = property.Type.ToString();

                // Check if the property type is an ICollection
                if (propertyType.StartsWith("ICollection<") || propertyType.StartsWith("IEnumerable<"))
                {
                    // Extract the element type from the ICollection
                    string elementType = propertyType.Substring(propertyType.IndexOf('<') + 1, propertyType.LastIndexOf('>') - propertyType.IndexOf('<') - 1);
                    string rule = GetFakerRuleForType(elementType, propertyName, modelClassName, true).Item1;

                    // Generate the RuleFor using f.Make
                    extendedFakerStringBuilder.AppendLine($@"                .RuleFor(o => o.{propertyName}, (f, o) => f.Make(f.Random.Int(1, 3), () => {rule}))");
                }
                else
                {
                    (string fakerRule, bool isPrimitive) rule = GetFakerRuleForType(propertyType, propertyName, modelClassName);
                    if (rule.isPrimitive)
                    {
                        primitiveFakerStringBuilder.AppendLine($@"                .RuleFor(o => o.{propertyName}, (f, o) => {rule.fakerRule})");
                    }
                    else
                    {
                        extendedFakerStringBuilder.AppendLine($@"                .RuleFor(o => o.{propertyName}, (f, o) => {rule.fakerRule})");
                    }
                }
            }
            primitiveFakerStringBuilder.AppendLine($@"                .UseSeed(20241112);");
            primitiveFakerStringBuilder.AppendLine($@"        }}");

            extendedFakerStringBuilder.AppendLine($@"            ;");
            extendedFakerStringBuilder.AppendLine($@"        }}");

            primitiveFakerStringBuilder.Append(extendedFakerStringBuilder);
            primitiveFakerStringBuilder.Append(extensionsFakerStringBuilder);

            primitiveFakerStringBuilder.AppendLine($@"    }}");

            primitiveFakerStringBuilder.AppendLine($@"}}");

            return primitiveFakerStringBuilder.ToString();
        }

        private static (string, bool) GetFakerRuleForType(string propertyType, string propertyName, string modelClassName, bool isCollection = false)
        {
            if (propertyType == "int" || propertyType == "int?")
            {
                return ("f.Random.Int()", true);
            }
            else if (propertyType == "long" || propertyType == "long?")
            {
                return ("f.Random.Long()", true);
            }
            else if (propertyType == "string" || propertyType == "string?")
            {
                return ("f.Lorem.Word()", true);
            }
            else if (propertyType == "DateTime" || propertyType == "DateTime?")
            {
                return ("f.Date.Past()", true);
            }
            else if (propertyType == "bool" || propertyType == "bool?")
            {
                return ("f.Random.Bool()", true);
            }
            else if (propertyType == "double" || propertyType == "double?")
            {
                return ("f.Random.Double()", true);
            }
            else if (propertyType == "Guid" || propertyType == "Guid?")
            {
                return ("f.Random.Guid()", true);
            }
            else if (propertyType == "decimal" || propertyType == "decimal?")
            {
                return ("f.Random.Decimal()", true);
            }
            else
            {
                string extensionMethod = CreateExtensionMethod(propertyType, propertyName, modelClassName, isCollection);
                // Only add the extension method once per type
                if (!extensionsFakerStringBuilder.ToString().Contains(extensionMethod.Trim()))
                {
                    extensionsFakerStringBuilder.AppendLine(extensionMethod);
                }

                return ($"{propertyType}Faker.Primitive().With{modelClassName}(o).Generate()", false);
            }
        }

        private static string CreateExtensionMethod(string propertyType, string propertyName, string modelClassName, bool isCollection)
        {
            // Generate extension method
            string extensionMethodName = $"With{propertyName}";
            propertyType = isCollection ? "IEnumerable<" + propertyType + ">" : propertyType;
            string extensionMethod = $@"
        /// <summary>
        /// Sets the <see cref=""{propertyType}""/> for the <see cref=""{modelClassName}""/>
        /// Use it directly from tests or from fakers definition other than Primitives
        /// </summary>
        /// <param name=""faker"">{modelClassName} faker</param>
        /// <param name=""{propertyName.ToCamelCase()}"">The property value</param>
        /// <returns></returns>
        public static Faker<{modelClassName}> {extensionMethodName}(this Faker<{modelClassName}> faker, {propertyType} {propertyName.ToCamelCase()})
        {{
            return faker.RuleFor(o => o.{propertyName}, {propertyName.ToCamelCase()});
        }}";
            return extensionMethod;
        }

        private static string ToCamelCase(this string input)
        {
            return System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(input);
        }
    }
}