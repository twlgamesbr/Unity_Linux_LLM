using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>
/// AGENTS.md §1.4: Allman brace style — opening braces on their own new line.
/// Scoped to statement blocks, type declarations, enums, namespaces, and
/// switch statements (object/collection initializers and accessor lists like
/// "{ get; set; }" are intentionally excluded — they're idiomatic single-line).
/// </summary>
public sealed class AllmanBraceRule : IReviewRule
{
    public string Id => "FMT05";
    public string Title => "Allman brace style";
    public string AgentsMdReference => "§1.4 Formatting Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var token in file.Root.DescendantTokens())
        {
            if (!token.IsKind(SyntaxKind.OpenBraceToken))
            {
                continue;
            }

            bool applies = token.Parent is BlockSyntax
                or TypeDeclarationSyntax
                or EnumDeclarationSyntax
                or NamespaceDeclarationSyntax
                or SwitchStatementSyntax;

            if (!applies)
            {
                continue;
            }

            var previousToken = token.GetPreviousToken();
            if (previousToken.IsKind(SyntaxKind.None))
            {
                continue;
            }

            if (file.LineOfToken(previousToken) == file.LineOfToken(token))
            {
                yield return new Finding(
                    Id,
                    Severity.Warning,
                    file.RelativePath,
                    file.LineOfToken(token),
                    "Opening brace should be on its own line (Allman style)."
                );
            }
        }
    }
}
