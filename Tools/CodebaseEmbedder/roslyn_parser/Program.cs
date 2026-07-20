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

        var allSymbols = new List<(string StableKey, string FullyQualifiedName, string Kind)>();
        var results = new List<object>();
        var filePaths = new HashSet<string>();

        foreach (var file in files)
        {
            var sourceText = await File.ReadAllTextAsync(file);
            var tree = CSharpSyntaxTree.ParseText(sourceText, options, path: file);
            var root = await tree.GetRootAsync();
            var relativePath = Path.GetRelativePath(projectRoot, file)
                .Replace(Path.DirectorySeparatorChar, '/');
            filePaths.Add(relativePath);
            var (fileResults, fileSymbols) = AnalyzeFile(
                projectRoot,
                relativePath,
                sourceText,
                root
            );
            results.AddRange(fileResults);
            allSymbols.AddRange(fileSymbols);
        }

        // Cross-file symbol resolution: build lookup
        var symbolLookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stableKey, fqn, kind) in allSymbols)
        {
            // Index by the fully-qualified name
            if (!symbolLookup.ContainsKey(fqn))
                symbolLookup[fqn] = new();
            symbolLookup[fqn].Add(stableKey);

            // Also index by simple name (last dot-segment)
            var simpleName = fqn.Split('.').LastOrDefault() ?? fqn;
            if (simpleName != fqn && !string.IsNullOrEmpty(simpleName))
            {
                if (!symbolLookup.ContainsKey(simpleName))
                    symbolLookup[simpleName] = new();
                symbolLookup[simpleName].Add(stableKey);
            }

            // Also index by type-qualified name (e.g. NPCDialogueManager.InitializeAsync)
            var fqnParts = fqn.Split('.');
            if (fqnParts.Length >= 2)
            {
                var typeQualified = string.Join(".", fqnParts[^2..]);
                if (typeQualified != fqn && !string.IsNullOrEmpty(typeQualified))
                {
                    if (!symbolLookup.ContainsKey(typeQualified))
                        symbolLookup[typeQualified] = new();
                    symbolLookup[typeQualified].Add(stableKey);
                }
            }
        }

        // Resolve unresolved call targets
        foreach (var obj in results)
        {
            if (
                obj is RelationRecord rel
                && rel.RelationKind == "calls"
                && !string.IsNullOrEmpty(rel.Target)
            )
            {
                var targetName = rel.Target.Split('(')[0].Trim();
                if (symbolLookup.TryGetValue(targetName, out var candidates))
                {
                    rel.Payload["resolved_to"] = candidates[0];
                    rel.Payload["resolution_count"] = candidates.Count;
                }
            }
        }

        // Emit symbol index as a synthetic record
        var symbolIndex = new Dictionary<string, object>
        {
            ["record_type"] = "symbol_index",
            ["stable_key"] = "symbol_index:global",
            ["total_symbols"] = allSymbols.Count,
            ["files_scanned"] = filePaths.Count,
            ["files"] = filePaths.ToList(),
        };
        results.Add(
            new FileRecord
            {
                RecordType = "symbol_index",
                StableKey = "symbol_index:global",
                Text = $"Symbol index: {allSymbols.Count} symbols across {filePaths.Count} files",
                Payload = symbolIndex,
            }
        );

        var json = JsonSerializer.Serialize(
            results,
            new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }
        );
        await Console.Out.WriteLineAsync(json);
        return 0;
    }

    private static (
        List<object> Records,
        List<(string StableKey, string FQN, string Kind)> Symbols
    ) AnalyzeFile(string projectRoot, string relPath, string text, SyntaxNode root)
    {
        var records = new List<object>();
        var symbols = new List<(string StableKey, string FQN, string Kind)>();

        var usings = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Select(u => u.Name?.ToString() ?? string.Empty)
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
            ["root_namespace"] = declaredNamespaces.FirstOrDefault() ?? string.Empty,
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
        // Extend member_names for properties and events for the file_overview
        var allMemberNames = new HashSet<string>((List<string>)filePayload["member_names"]);
        foreach (var t in typeDeclarations)
        {
            foreach (var prop in t.Members.OfType<PropertyDeclarationSyntax>())
                allMemberNames.Add(prop.Identifier.Text);
            foreach (var evt in t.Members.OfType<EventDeclarationSyntax>())
                allMemberNames.Add(evt.Identifier.Text);
        }
        filePayload["member_names"] = allMemberNames.OrderBy(n => n).ToList();

        records.Add(
            new FileRecord
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
                        $"Types: {string.Join(", ", filePayload["type_names"] as List<string> ?? new())}",
                        $"Members: {string.Join(", ", allMemberNames)}",
                    }
                ),
                Payload = filePayload,
            }
        );
        symbols.Add(($"file:{relPath}", relPath, "file"));

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
            records.Add(
                new FileRecord
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
                }
            );
            symbols.Add(($"namespace:{ns}:{relPath}", ns, "namespace"));
        }

        // --- USING DIRECTIVES + namespace-uses-namespace ---
        var usingNodes = root.DescendantNodes().OfType<UsingDirectiveSyntax>().ToList();
        for (var usingIndex = 0; usingIndex < usingNodes.Count; usingIndex++)
        {
            var usingNode = usingNodes[usingIndex];
            var usingNs = usingNode.Name?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(usingNs))
                continue;
            var usingLine = usingNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var usingPayload = new Dictionary<string, object>(filePayload)
            {
                ["using_namespace"] = usingNs,
                ["symbol_kind"] = "using_directive",
                ["line_start"] = usingLine,
                ["line_end"] = usingLine,
            };
            records.Add(
                new FileRecord
                {
                    RecordType = "using_directive",
                    StableKey = $"using:{usingNs}:{relPath}:{usingLine}",
                    Text = string.Join(
                        "\n",
                        new[]
                        {
                            $"Using directive {usingNs}",
                            $"Path {relPath}",
                            $"Assembly -",
                            $"Declared namespaces: {string.Join(", ", declaredNamespaces)}",
                            $"Types in file: {string.Join(", ", filePayload["type_names"] as List<string> ?? new())}",
                        }
                    ),
                    Payload = usingPayload,
                }
            );
            foreach (var declaredNs in declaredNamespaces)
            {
                records.Add(
                    new RelationRecord
                    {
                        RelationKind = "namespace-uses-namespace",
                        Source = declaredNs,
                        Target = usingNs,
                        Path = relPath,
                        Payload = new Dictionary<string, object> { ["asmdef"] = string.Empty },
                    }
                );
            }
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

            var fqTypeName = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
            var payload = new Dictionary<string, object>(filePayload)
            {
                ["namespace"] = ns,
                ["type_name"] = typeName,
                ["fq_name"] = fqTypeName,
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

            records.Add(
                new FileRecord
                {
                    RecordType = "type",
                    StableKey = $"type:{fqTypeName}:{relPath}",
                    Text = string.Join("\n", textParts),
                    Payload = payload,
                }
            );
            symbols.Add(($"type:{fqTypeName}:{relPath}", fqTypeName, "type"));

            if (!string.IsNullOrEmpty(ns))
            {
                records.Add(
                    new RelationRecord
                    {
                        RelationKind = "namespace-contains-type",
                        Source = ns,
                        Target = fqTypeName,
                        Path = relPath,
                        Payload = new Dictionary<string, object> { ["asmdef"] = string.Empty },
                    }
                );
            }
            foreach (var baseType in baseTypes)
            {
                records.Add(
                    new RelationRecord
                    {
                        RelationKind = "inherits",
                        Source = fqTypeName,
                        Target = NormalizeTypeName(baseType),
                        Path = relPath,
                        Payload = new Dictionary<string, object> { ["asmdef"] = string.Empty },
                    }
                );
                EmitTypeUses(records, fqTypeName, baseType, relPath, "base_type");
            }
            foreach (var iface in interfaces)
            {
                records.Add(
                    new RelationRecord
                    {
                        RelationKind = "implements",
                        Source = fqTypeName,
                        Target = NormalizeTypeName(iface),
                        Path = relPath,
                        Payload = new Dictionary<string, object> { ["asmdef"] = string.Empty },
                    }
                );
                EmitTypeUses(records, fqTypeName, iface, relPath, "interface");
            }

            // --- METHODS ---
            foreach (var member in typeDeclaration.Members.OfType<MethodDeclarationSyntax>())
            {
                var memberName = member.Identifier.Text;
                var paramTypes = member
                    .ParameterList.Parameters.Select(p => p.Type?.ToString() ?? string.Empty)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();
                EmitMember(
                    records,
                    symbols,
                    relPath,
                    filePayload,
                    ns,
                    typeName,
                    fqTypeName,
                    member,
                    memberName,
                    "method",
                    member.Modifiers.ToString()
                        + " "
                        + member.ReturnType
                        + " "
                        + memberName
                        + member.ParameterList,
                    member.ReturnType.ToString(),
                    paramTypes
                );
            }

            // --- CONSTRUCTORS ---
            foreach (var ctor in typeDeclaration.Members.OfType<ConstructorDeclarationSyntax>())
            {
                var ctorName = ctor.Identifier.Text;
                var signature = ctor.Modifiers + " " + ctorName + ctor.ParameterList;
                EmitMember(
                    records,
                    symbols,
                    relPath,
                    filePayload,
                    ns,
                    typeName,
                    fqTypeName,
                    ctor,
                    ctorName,
                    "constructor",
                    signature
                );
            }

            // --- PROPERTIES ---
            foreach (var prop in typeDeclaration.Members.OfType<PropertyDeclarationSyntax>())
            {
                var propName = prop.Identifier.Text;
                var hasGetter =
                    prop.AccessorList?.Accessors.Any(a =>
                        a.IsKind(SyntaxKind.GetAccessorDeclaration)
                    )
                    ?? false;
                var hasSetter =
                    prop.AccessorList?.Accessors.Any(a =>
                        a.IsKind(SyntaxKind.SetAccessorDeclaration)
                    )
                    ?? false;
                var hasInit =
                    prop.AccessorList?.Accessors.Any(a =>
                        a.IsKind(SyntaxKind.InitAccessorDeclaration)
                    )
                    ?? false;
                var isAutoProp =
                    prop.AccessorList?.Accessors.All(a =>
                        a.Body == null && a.ExpressionBody == null
                    ) ?? false;
                var accessorSummary =
                    $"get:{(hasGetter ? "Y" : "N")} set:{(hasSetter ? "Y" : "N")} init:{(hasInit ? "Y" : "N")} auto:{(isAutoProp ? "Y" : "N")}";
                var signature =
                    prop.Modifiers
                    + " "
                    + prop.Type
                    + " "
                    + propName
                    + " { "
                    + accessorSummary
                    + " }";

                var line = prop.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var endLine = prop.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
                var fq = $"{fqTypeName}.{propName}";
                var propType = prop.Type.ToString();
                var memberPayload = new Dictionary<string, object>(filePayload)
                {
                    ["namespace"] = ns,
                    ["type_name"] = typeName,
                    ["member_name"] = propName,
                    ["symbol_kind"] = "property",
                    ["signature"] = string.Join(
                        " ",
                        signature.Split(
                            new[] { ' ', '\t', '\r', '\n' },
                            StringSplitOptions.RemoveEmptyEntries
                        )
                    ),
                    ["return_type"] = propType,
                    ["line_start"] = line,
                    ["line_end"] = endLine,
                    ["has_getter"] = hasGetter,
                    ["has_setter"] = hasSetter,
                    ["has_init"] = hasInit,
                    ["is_auto_property"] = isAutoProp,
                };

                records.Add(
                    new FileRecord
                    {
                        RecordType = "member",
                        StableKey = $"member:{fq}:{line}:{relPath}",
                        Text =
                            $"{prop.Type} {propName} {{ {accessorSummary} }}\n{relPath}:{line}-{endLine}\nType {typeName} in {ns}",
                        Payload = memberPayload,
                    }
                );
                symbols.Add(($"member:{fq}:{line}:{relPath}", fq, "property"));

                records.Add(
                    new RelationRecord
                    {
                        RelationKind = "type-contains-member",
                        Source = fqTypeName,
                        Target = fq,
                        Path = relPath,
                        Payload = new Dictionary<string, object>
                        {
                            ["asmdef"] = string.Empty,
                            ["member_kind"] = "property",
                        },
                    }
                );
                EmitTypeUses(records, fqTypeName, propType, relPath, "property");
            }

            // --- EVENTS ---
            foreach (var evt in typeDeclaration.Members.OfType<EventDeclarationSyntax>())
            {
                var evtName = evt.Identifier.Text;
                var line = evt.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var endLine = evt.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
                var fq = $"{fqTypeName}.{evtName}";
                var signature = evt.Modifiers + " event " + evt.Type + " " + evtName;
                var memberPayload = new Dictionary<string, object>(filePayload)
                {
                    ["namespace"] = ns,
                    ["type_name"] = typeName,
                    ["member_name"] = evtName,
                    ["symbol_kind"] = "event",
                    ["signature"] = string.Join(
                        " ",
                        signature.Split(
                            new[] { ' ', '\t', '\r', '\n' },
                            StringSplitOptions.RemoveEmptyEntries
                        )
                    ),
                    ["line_start"] = line,
                    ["line_end"] = endLine,
                };

                records.Add(
                    new FileRecord
                    {
                        RecordType = "member",
                        StableKey = $"member:{fq}:{line}:{relPath}",
                        Text = $"{signature}\n{relPath}:{line}-{endLine}\nType {typeName} in {ns}",
                        Payload = memberPayload,
                    }
                );
                symbols.Add(($"member:{fq}:{line}:{relPath}", fq, "event"));

                records.Add(
                    new RelationRecord
                    {
                        RelationKind = "type-contains-member",
                        Source = fqTypeName,
                        Target = fq,
                        Path = relPath,
                        Payload = new Dictionary<string, object>
                        {
                            ["asmdef"] = string.Empty,
                            ["member_kind"] = "event",
                        },
                    }
                );
            }

            // --- ALL FIELDS (not just serialized) ---
            foreach (var field in typeDeclaration.Members.OfType<FieldDeclarationSyntax>())
            {
                var isSerialized =
                    field
                        .AttributeLists.SelectMany(a => a.Attributes)
                        .Any(a =>
                            a.Name.ToString()
                                .EndsWith("SerializeField", StringComparison.OrdinalIgnoreCase)
                        ) || field.Modifiers.Any(SyntaxKind.PublicKeyword);
                foreach (var variable in field.Declaration.Variables)
                {
                    var fieldName = variable.Identifier.Text;
                    var line = field.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var endLine = field.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
                    var fq = $"{fqTypeName}.{fieldName}";
                    var fieldType = field.Declaration.Type.ToString();
                    var payloadField = new Dictionary<string, object>(filePayload)
                    {
                        ["namespace"] = ns,
                        ["type_name"] = typeName,
                        ["member_name"] = fieldName,
                        ["symbol_kind"] = "field",
                        ["field_type"] = fieldType,
                        ["line_start"] = line,
                        ["line_end"] = endLine,
                        ["is_serialized"] = isSerialized,
                        ["attributes"] = field
                            .AttributeLists.SelectMany(a => a.Attributes)
                            .Select(a => a.Name.ToString())
                            .Distinct()
                            .ToList(),
                        ["signature"] = $"{field.Modifiers} {fieldType} {fieldName}".Trim(),
                    };
                    var recordType = isSerialized ? "serialized_field" : "field";
                    records.Add(
                        new FileRecord
                        {
                            RecordType = recordType,
                            StableKey = $"{recordType}:{fq}:{relPath}",
                            Text =
                                $"{(isSerialized ? "Serialized" : "Non-serialized")} field {typeName}.{fieldName} : {fieldType} in {relPath}\nOwning type {typeName}, Namespace {ns}",
                            Payload = payloadField,
                        }
                    );
                    symbols.Add(($"{recordType}:{fq}:{relPath}", fq, "field"));

                    records.Add(
                        new RelationRecord
                        {
                            RelationKind = "type-contains-member",
                            Source = fqTypeName,
                            Target = fq,
                            Path = relPath,
                            Payload = new Dictionary<string, object>
                            {
                                ["asmdef"] = string.Empty,
                                ["member_kind"] = "field",
                                ["is_serialized"] = isSerialized,
                            },
                        }
                    );
                    EmitTypeUses(records, fqTypeName, fieldType, relPath, "field");
                }
            }

            // --- CALL EXPRESSIONS ---
            var callExpressions = typeDeclaration
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>();
            foreach (var call in callExpressions)
            {
                var target = call.Expression.ToString();
                if (string.IsNullOrWhiteSpace(target))
                    continue;
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
                    continue;

                var line = call.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                records.Add(
                    new RelationRecord
                    {
                        RelationKind = "calls",
                        Source = fqTypeName,
                        Target = target,
                        Path = relPath,
                        Payload = new Dictionary<string, object>
                        {
                            ["asmdef"] = string.Empty,
                            ["line_start"] = line,
                        },
                    }
                );
            }
        }

        return (records, symbols);
    }

    private static void EmitMember(
        List<object> records,
        List<(string StableKey, string FQN, string Kind)> symbols,
        string relPath,
        Dictionary<string, object> filePayload,
        string ns,
        string typeName,
        string fqTypeName,
        SyntaxNode member,
        string memberName,
        string kind,
        string rawSignature,
        string? returnType = null,
        List<string>? parameterTypes = null
    )
    {
        var line = member.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var endLine = member.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
        var fq = string.IsNullOrEmpty(ns)
            ? $"{typeName}.{memberName}"
            : $"{ns}.{typeName}.{memberName}";

        // Extract attributes
        var attributes = new List<string>();
        if (member is MemberDeclarationSyntax memberDecl)
        {
            attributes = memberDecl
                .AttributeLists.SelectMany(a => a.Attributes)
                .Select(a => a.Name.ToString())
                .Distinct()
                .ToList();
        }

        var memberPayload = new Dictionary<string, object>(filePayload)
        {
            ["namespace"] = ns,
            ["type_name"] = typeName,
            ["member_name"] = memberName,
            ["symbol_kind"] = kind,
            ["signature"] = string.Join(
                " ",
                rawSignature.Split(
                    new[] { ' ', '\t', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries
                )
            ),
            ["line_start"] = line,
            ["line_end"] = endLine,
            ["attributes"] = attributes,
        };
        if (!string.IsNullOrWhiteSpace(returnType))
            memberPayload["return_type"] = returnType;
        if (parameterTypes is { Count: > 0 })
            memberPayload["parameter_types"] = parameterTypes;

        records.Add(
            new FileRecord
            {
                RecordType = "member",
                StableKey = $"member:{fq}:{line}:{relPath}",
                Text =
                    $"{memberPayload["signature"]}\n{relPath}:{line}-{endLine}\nType {typeName} in {ns}",
                Payload = memberPayload,
            }
        );
        symbols.Add(($"member:{fq}:{line}:{relPath}", fq, kind));

        records.Add(
            new RelationRecord
            {
                RelationKind = "type-contains-member",
                Source = fqTypeName,
                Target = fq,
                Path = relPath,
                Payload = new Dictionary<string, object>
                {
                    ["asmdef"] = string.Empty,
                    ["member_kind"] = kind,
                },
            }
        );

        if (!string.IsNullOrWhiteSpace(returnType))
            EmitTypeUses(records, fqTypeName, returnType, relPath, "return");
        if (parameterTypes != null)
        {
            foreach (var paramType in parameterTypes)
                EmitTypeUses(records, fqTypeName, paramType, relPath, "parameter");
        }
    }

    private static readonly HashSet<string> PrimitiveTypeNames = new(StringComparer.Ordinal)
    {
        "void",
        "bool",
        "byte",
        "sbyte",
        "char",
        "decimal",
        "double",
        "float",
        "int",
        "uint",
        "long",
        "ulong",
        "short",
        "ushort",
        "object",
        "string",
        "dynamic",
        "var",
        "Task",
        "UniTask",
        "CancellationToken",
        "Action",
        "Func",
    };

    private static string NormalizeTypeName(string typeName)
    {
        var cleaned = typeName.Trim();
        var generic = cleaned.IndexOf('<');
        if (generic >= 0)
            cleaned = cleaned[..generic];
        cleaned = cleaned.TrimEnd('?', '[', ']').Trim();
        return cleaned;
    }

    private static IEnumerable<string> ExtractTypeNames(string typeText)
    {
        if (string.IsNullOrWhiteSpace(typeText))
            yield break;
        // Split generic args and arrays: List<Foo>, Foo[], Dictionary<A,B>
        foreach (
            var part in typeText.Split(
                new[] { '<', '>', ',', '[', ']', ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries
            )
        )
        {
            var name = NormalizeTypeName(part);
            if (string.IsNullOrWhiteSpace(name))
                continue;
            if (PrimitiveTypeNames.Contains(name))
                continue;
            if (name is "System" or "UnityEngine" or "Unity" or "NPCSystem")
                continue;
            yield return name;
        }
    }

    private static void EmitTypeUses(
        List<object> records,
        string sourceFq,
        string typeText,
        string relPath,
        string via
    )
    {
        foreach (var target in ExtractTypeNames(typeText).Distinct(StringComparer.Ordinal))
        {
            records.Add(
                new RelationRecord
                {
                    RelationKind = "type-uses-type",
                    Source = sourceFq,
                    Target = target,
                    Path = relPath,
                    Payload = new Dictionary<string, object>
                    {
                        ["asmdef"] = string.Empty,
                        ["via"] = via,
                        ["raw_type"] = typeText,
                    },
                }
            );
        }
    }
}
