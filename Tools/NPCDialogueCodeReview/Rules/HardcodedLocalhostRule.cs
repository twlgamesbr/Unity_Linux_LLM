using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>
/// AGENTS.md §1.6: no hard-coded "localhost" strings — use
/// NPCNetworkUtils.IsLocalHost(host), which checks both "localhost" and
/// "127.0.0.1" case-insensitively. (Historically documented as
/// NPCFlowLogger.IsLocalHost — the helper has since moved; see the
/// companion NPC-DOC01 drift note this tool's README calls out.)
/// Two tiers: direct comparisons against the literal are a real correctness
/// risk (Warning); plain default-value assignments are lower risk (Suggestion).
/// </summary>
public sealed class HardcodedLocalhostRule : IReviewRule
{
    public string Id => "NET01";
    public string Title => "No hard-coded \"localhost\" comparisons/defaults";
    public string AgentsMdReference => "§1.6 Key Anti-Pattern Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        if (file.RelativePath.EndsWith("NPCNetworkUtils.cs", StringComparison.Ordinal))
        {
            yield break; // The canonical helper is allowed to define the literal.
        }

        foreach (var literal in file.Root.DescendantNodes().OfType<LiteralExpressionSyntax>())
        {
            if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                continue;
            }

            string? value = literal.Token.ValueText;
            if (value is null || !value.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool isComparison = IsComparisonContext(literal);

            yield return new Finding(
                isComparison ? "NET01" : "NET02",
                isComparison ? Severity.Warning : Severity.Suggestion,
                file.RelativePath,
                file.LineOf(literal),
                isComparison
                    ? "Literal \"localhost\" used in a comparison — use NPCNetworkUtils.IsLocalHost(host) " +
                      "so \"127.0.0.1\" is also handled."
                    : "Literal \"localhost\" used as a default value — acceptable as a config default, " +
                      "but any comparison against it downstream should go through NPCNetworkUtils.IsLocalHost(host)."
            );
        }
    }

    static bool IsComparisonContext(LiteralExpressionSyntax literal)
    {
        if (literal.Parent is BinaryExpressionSyntax binary &&
            (binary.IsKind(SyntaxKind.EqualsExpression) ||
             binary.IsKind(SyntaxKind.NotEqualsExpression)))
        {
            return true;
        }

        if (literal.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation } })
        {
            string expr = invocation.Expression.ToString();
            return expr.EndsWith(".Equals", StringComparison.Ordinal)
                || expr.EndsWith(".Contains", StringComparison.Ordinal)
                || expr.EndsWith(".StartsWith", StringComparison.Ordinal)
                || expr.EndsWith(".EndsWith", StringComparison.Ordinal);
        }

        return false;
    }
}
