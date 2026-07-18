using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>
/// AGENTS.md §1.1: private fields must use "_camelCase" (leading underscore).
/// Applies to fields with an explicit `private` modifier, or no accessibility
/// modifier at all (C# default field accessibility is private).
/// </summary>
public sealed class PrivateFieldNamingRule : IReviewRule
{
    public string Id => "NAM01";
    public string Title => "Private field naming (_camelCase)";
    public string AgentsMdReference => "§1.1 Naming Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var fieldDecl in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            var modifiers = fieldDecl.Modifiers;
            bool hasExplicitAccessibility = modifiers.Any(m =>
                m.IsKind(SyntaxKind.PublicKeyword)
                || m.IsKind(SyntaxKind.InternalKeyword)
                || m.IsKind(SyntaxKind.ProtectedKeyword));
            bool isConst = modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword));

            if (hasExplicitAccessibility || isConst)
            {
                continue;
            }

            foreach (var variable in fieldDecl.Declaration.Variables)
            {
                string name = variable.Identifier.Text;
                if (!NamingUtils.IsPrivateFieldCase(name))
                {
                    yield return new Finding(
                        Id,
                        Severity.Warning,
                        file.RelativePath,
                        file.LineOf(variable),
                        $"Private field '{name}' should be named '_{ToCamelSuggestion(name)}' " +
                        "(leading underscore + camelCase)."
                    );
                }
            }
        }
    }

    static string ToCamelSuggestion(string name)
    {
        string trimmed = name.TrimStart('_');
        if (trimmed.Length == 0)
        {
            return name;
        }

        return char.ToLowerInvariant(trimmed[0]) + trimmed[1..];
    }
}
