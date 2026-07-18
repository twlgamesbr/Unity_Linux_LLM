using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>
/// AGENTS.md §1.5: public API surface should have /// &lt;summary&gt; docs.
/// Reported at Info severity — this is guidance, not a hard gate, and test
/// methods/overrides are excluded.
/// </summary>
public sealed class XmlDocRule : IReviewRule
{
    public string Id => "DOC01";
    public string Title => "Public API missing XML doc summary";
    public string AgentsMdReference => "§1.5 XML Documentation";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (!IsPublic(method.Modifiers) || IsOverrideOrTest(method))
            {
                continue;
            }

            if (!HasSummaryDoc(method))
            {
                yield return Violation(file, method, method.Identifier.Text, "method");
            }
        }

        foreach (var cls in file.Root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (!IsPublic(cls.Modifiers))
            {
                continue;
            }

            if (!HasSummaryDoc(cls))
            {
                yield return Violation(file, cls, cls.Identifier.Text, "class");
            }
        }
    }

    static bool IsPublic(SyntaxTokenList modifiers) => modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));

    static bool IsOverrideOrTest(MethodDeclarationSyntax method)
    {
        if (method.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)))
        {
            return true;
        }

        foreach (var list in method.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                string name = attr.Name.ToString();
                if (name.Contains("Test") || name.Contains("SetUp") || name.Contains("TearDown"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    static bool HasSummaryDoc(SyntaxNode node)
    {
        foreach (var trivia in node.GetLeadingTrivia())
        {
            if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                if (trivia.ToString().Contains("<summary>"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    Finding Violation(SourceFile file, SyntaxNode node, string name, string kind) =>
        new(
            Id,
            Severity.Info,
            file.RelativePath,
            file.LineOf(node),
            $"Public {kind} '{name}' has no /// <summary> doc comment."
        );
}
