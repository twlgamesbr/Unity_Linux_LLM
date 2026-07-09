using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodebaseRoslynParser;

public sealed class FileRecord
{
    public string RecordType { get; set; } = string.Empty;
    public string StableKey { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public Dictionary<string, object> Payload { get; set; } = new();
}

public sealed class RelationRecord
{
    public string RelationKind { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public Dictionary<string, object> Payload { get; set; } = new();
}

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine(
                "Usage: CodebaseRoslynParser <projectRoot> <file1.cs> [<file2.cs> ...]"
            );
            return 1;
        }

        var projectRoot = args[0];
        var projectName =
            args.Length >= 2 && !args[1].EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                ? args[1]
                : "Unity_Linux_LLM";
        var files = args.Skip(projectName == "Unity_Linux_LLM" ? 1 : 2).ToArray();

        var options = new CSharpParseOptions(
            LanguageVersion.CSharp12,
            DocumentationMode.Parse,
            kind: SourceCodeKind.Regular
        );
        var results = new List<object>();

        foreach (var file in files)
        {
            var sourceText = await File.ReadAllTextAsync(file).ConfigureAwait(false);
            var tree = CSharpSyntaxTree.ParseText(sourceText, options, path: file);
            var root = await tree.GetRootAsync().ConfigureAwait(false);
            var relativePath = Path.GetRelativePath(projectRoot, file)
                .Replace(Path.DirectorySeparatorChar, '/');
            results.AddRange(AnalyzeFile(projectRoot, relativePath, sourceText, root));
        }

        var json = JsonSerializer.Serialize(
            results,
            new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }
        );
        await Console.Out.WriteLineAsync(json).ConfigureAwait(false);
        return 0;
    }

    private static IEnumerable<object> AnalyzeFile(
        string projectRoot,
        string relPath,
        string text,
        SyntaxNode root
    )
    {
        var usings = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(u => u.Name.ToString())
            .Distinct()
            .OrderBy(u => u)
            .ToList();

        var declaredNamespaces = root.DescendantNodes()
            .OfType<NamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString())
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToList();
        var filePayload = new Dictionary<string, object>
        {
            ["project"] = "Unity_Linux_LLM",
            ["path"] = relPath,
            ["relative_dir"] = Path.GetDirectoryName(relPath) ?? string.Empty,
            ["unity_region"] = "Runtime",
            ["asmdef"] = string.Empty,
            ["asmdef_path"] = string.Empty,
            ["root_namespace"] = string.Empty,
            ["declared_namespaces"] = declaredNamespaces,
            ["using_directives"] = usings,
            ["type_names"] = typeDeclarations
                .Select(t => t.Identifier.Text)
                .Distinct()
                .OrderBy(t => t)
                .ToList(),
            ["member_names"] = typeDeclarations
                .SelectMany(t =>
                    t.Members.OfType<BaseMethodDeclarationSyntax>()
                        .Select(m =>
                            m is MethodDeclarationSyntax md ? md.Identifier.Text
                            : m is ConstructorDeclarationSyntax cd ? cd.Identifier.Text
                            : string.Empty
                        )
                )
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .OrderBy(n => n)
                .ToList(),
            ["line_start"] = 1,
            ["line_end"] = Math.Max(1, text.Count(c => c == '\n') + 1),
        };

        yield return new FileRecord
        {
            RecordType = "file_overview",
            StableKey = $"file:{relPath}",
            Text = string.Join(
                "\n",
                new[]
                {
                    $"File overview {relPath}",
                    $"Assembly -",
                    $"Region Runtime",
                    $"Namespaces: {string.Join(", ", declaredNamespaces)}",
                    $"Using directives: {string.Join(", ", usings)}",
                    $"Types: {string.Join(", ", typeDeclarations.Select(t => t.Identifier.Text).Distinct())}",
                    $"Members: {string.Join(", ", filePayload["member_names"] as List<string> ?? new())}",
                }
            ),
            Payload = filePayload,
        };

        foreach (
            var namespaceDeclaration in root.DescendantNodes().OfType<NamespaceDeclarationSyntax>()
        )
        {
            var ns = namespaceDeclaration.Name.ToString();
            var types = namespaceDeclaration
                .Members.OfType<TypeDeclarationSyntax>()
                .Select(t => t.Identifier.Text)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
            var payload = new Dictionary<string, object>(filePayload)
            {
                ["namespace"] = ns,
                ["declared_type_names"] = types,
                ["symbol_kind"] = "namespace",
                ["line_start"] =
                    namespaceDeclaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                ["line_end"] =
                    namespaceDeclaration.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
            };
            yield return new FileRecord
            {
                RecordType = "namespace",
                StableKey = $"namespace:{ns}:{relPath}:{payload["line_start"]}",
                Text = string.Join(
                    "\n",
                    new[]
                    {
                        $"Namespace {ns}",
                        $"Path {relPath}",
                        $"Assembly -",
                        $"Region Runtime",
                        $"Declared types: {string.Join(", ", types)}",
                        $"Using directives: {string.Join(", ", usings)}",
                    }
                ),
                Payload = payload,
            };
        }

        foreach (var typeDeclaration in typeDeclarations)
        {
            var ns =
                typeDeclaration
                    .Ancestors()
                    .OfType<NamespaceDeclarationSyntax>()
                    .FirstOrDefault()
                    ?.Name.ToString()
                ?? string.Empty;
            var typeName = typeDeclaration.Identifier.Text;
            var baseTypes =
                typeDeclaration.BaseList?.Types.Select(bt => bt.Type.ToString()).ToList()
                ?? new List<string>();
            var interfaces =
                typeDeclaration
                    .BaseList?.Types.Select(bt => bt.Type.ToString())
                    .Where(name => name.StartsWith("I") && name.Length > 1)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList()
                ?? new List<string>();
            var methodNames = typeDeclaration
                .Members.OfType<MethodDeclarationSyntax>()
                .Select(m => m.Identifier.Text)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            var summary = string.Empty;
            var trivia = typeDeclaration.GetLeadingTrivia().ToString();
            var summaryStart = trivia.IndexOf("<summary>", StringComparison.OrdinalIgnoreCase);
            if (summaryStart >= 0)
            {
                var summaryEnd = trivia.IndexOf(
                    "</summary>",
                    summaryStart,
                    StringComparison.OrdinalIgnoreCase
                );
                if (summaryEnd >= 0)
                {
                    summary = trivia[(summaryStart + 9)..summaryEnd].Trim();
                }
            }

            var payload = new Dictionary<string, object>(filePayload)
            {
                ["namespace"] = ns,
                ["type_name"] = typeName,
                ["symbol_kind"] = typeDeclaration.Keyword.Text,
                ["line_start"] =
                    typeDeclaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                ["line_end"] = typeDeclaration.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                ["attributes"] = typeDeclaration
                    .AttributeLists.SelectMany(a => a.Attributes)
                    .Select(a => a.Name.ToString())
                    .Distinct()
                    .ToList(),
                ["base_types"] = baseTypes,
                ["interfaces"] = interfaces,
            };
            var textParts = new List<string>
            {
                $"{typeDeclaration.Keyword.Text} {typeName}{(baseTypes.Any() ? " : " + string.Join(", ", baseTypes) : string.Empty)}",
                $"Assembly -",
                $"Namespace {ns}",
                $"Path {relPath}",
                $"Region Runtime",
                $"Purpose: {(typeName.ToLowerInvariant().Contains("manager") ? "dialogue orchestration and transport" : typeDeclaration.Keyword.Text)}",
            };
            if (!string.IsNullOrWhiteSpace(summary))
            {
                textParts.Add($"Summary: {summary}");
            }
            if (methodNames.Any())
            {
                textParts.Add($"Methods: {string.Join(", ", methodNames)}");
            }
            textParts.Add($"Base types: {(baseTypes.Any() ? string.Join(", ", baseTypes) : "-")}");
            textParts.Add(
                $"Interfaces: {(interfaces.Any() ? string.Join(", ", interfaces) : "-")}"
            );

            yield return new FileRecord
            {
                RecordType = "type",
                StableKey = $"type:{ns}.{typeName}:{relPath}",
                Text = string.Join("\n", textParts),
                Payload = payload,
            };

            if (!string.IsNullOrEmpty(ns))
            {
                yield return new RelationRecord
                {
                    RelationKind = "namespace-contains-type",
                    Source = ns,
                    Target = $"{ns}.{typeName}",
                    Path = relPath,
                    Payload = new Dictionary<string, object> { ["asmdef"] = string.Empty },
                };
            }
            foreach (var baseType in baseTypes)
            {
                yield return new RelationRecord
                {
                    RelationKind = "inherits",
                    Source = $"{ns}.{typeName}",
                    Target = baseType,
                    Path = relPath,
                    Payload = new Dictionary<string, object> { ["asmdef"] = string.Empty },
                };
            }
            foreach (var iface in interfaces)
            {
                yield return new RelationRecord
                {
                    RelationKind = "implements",
                    Source = $"{ns}.{typeName}",
                    Target = iface,
                    Path = relPath,
                    Payload = new Dictionary<string, object> { ["asmdef"] = string.Empty },
                };
            }

            foreach (var member in typeDeclaration.Members.OfType<MethodDeclarationSyntax>())
            {
                var memberName = member.Identifier.Text;
                var signature =
                    member.Modifiers.ToString()
                    + " "
                    + member.ReturnType
                    + " "
                    + memberName
                    + member.ParameterList;
                var payloadMember = new Dictionary<string, object>(filePayload)
                {
                    ["namespace"] = ns,
                    ["type_name"] = typeName,
                    ["member_name"] = memberName,
                    ["symbol_kind"] = "method",
                    ["signature"] = string.Join(
                        " ",
                        signature.Split(
                            new[] { ' ', '\t', '\r', '\n' },
                            StringSplitOptions.RemoveEmptyEntries
                        )
                    ),
                    ["line_start"] = member.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    ["line_end"] = member.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                    ["attributes"] = member
                        .AttributeLists.SelectMany(a => a.Attributes)
                        .Select(a => a.Name.ToString())
                        .Distinct()
                        .ToList(),
                };
                var fq = string.IsNullOrEmpty(ns)
                    ? $"{typeName}.{memberName}"
                    : $"{ns}.{typeName}.{memberName}";
                yield return new FileRecord
                {
                    RecordType = "member",
                    StableKey = $"member:{fq}:{payloadMember["line_start"]}:{relPath}",
                    Text =
                        payloadMember["signature"].ToString()
                        + "\n"
                        + relPath
                        + ":"
                        + payloadMember["line_start"]
                        + "-"
                        + payloadMember["line_end"]
                        + "\nType "
                        + typeName
                        + " in "
                        + ns,
                    Payload = payloadMember,
                };
                yield return new RelationRecord
                {
                    RelationKind = "type-contains-member",
                    Source = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}",
                    Target = fq,
                    Path = relPath,
                    Payload = new Dictionary<string, object> { ["asmdef"] = string.Empty },
                };
            }

            foreach (var field in typeDeclaration.Members.OfType<FieldDeclarationSyntax>())
            {
                var isSerialized =
                    field
                        .AttributeLists.SelectMany(a => a.Attributes)
                        .Any(a =>
                            a.Name.ToString()
                                .EndsWith("SerializeField", StringComparison.OrdinalIgnoreCase)
                        ) || field.Modifiers.Any(SyntaxKind.PublicKeyword);
                if (!isSerialized)
                {
                    continue;
                }
                foreach (var variable in field.Declaration.Variables)
                {
                    var fieldName = variable.Identifier.Text;
                    var line = field.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var payloadField = new Dictionary<string, object>(filePayload)
                    {
                        ["namespace"] = ns,
                        ["type_name"] = typeName,
                        ["member_name"] = fieldName,
                        ["symbol_kind"] = "field",
                        ["line_start"] = line,
                        ["line_end"] = line,
                        ["attributes"] = field
                            .AttributeLists.SelectMany(a => a.Attributes)
                            .Select(a => a.Name.ToString())
                            .Distinct()
                            .ToList(),
                        ["signature"] =
                            $"{field.Modifiers} {field.Declaration.Type} {fieldName}".Trim(),
                    };
                    yield return new FileRecord
                    {
                        RecordType = "serialized_field",
                        StableKey = $"serialized_field:{ns}.{typeName}.{fieldName}:{relPath}",
                        Text =
                            $"Serialized field {typeName}.{fieldName} in {relPath}\nType {typeName}, Namespace {ns}",
                        Payload = payloadField,
                    };
                }
            }

            var callExpressions = typeDeclaration
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>();
            foreach (var call in callExpressions)
            {
                var target = call.Expression.ToString();
                if (string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }
                if (
                    new[]
                    {
                        "if",
                        "for",
                        "foreach",
                        "while",
                        "switch",
                        "catch",
                        "using",
                        "nameof",
                        "typeof",
                        "new",
                    }.Contains(target)
                )
                {
                    continue;
                }

                var line = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                yield return new RelationRecord
                {
                    RelationKind = "calls",
                    Source = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}",
                    Target = target,
                    Path = relPath,
                    Payload = new Dictionary<string, object>
                    {
                        ["asmdef"] = string.Empty,
                        ["line_start"] = line,
                    },
                };
            }
        }
    }
}
