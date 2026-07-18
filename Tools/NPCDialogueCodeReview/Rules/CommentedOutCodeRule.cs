using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>
/// AGENTS.md §1.6: no commented-out code — delete it. This is a heuristic
/// (regex pattern-matching on single-line comment trivia), so findings are
/// reported at Suggestion severity for a human to confirm — prose comments
/// occasionally trip the heuristic.
/// </summary>
public sealed class CommentedOutCodeRule : IReviewRule
{
    static readonly Regex EndsWithSemicolon = new(@";\s*$", RegexOptions.Compiled);
    static readonly Regex MethodCallShape = new(@"^[A-Za-z_][A-Za-z0-9_.]*\s*\([^)]*\)\s*;?$", RegexOptions.Compiled);
    static readonly Regex CodeKeywordStart = new(
        @"^(if|for|foreach|while|switch|public|private|protected|internal|static|void|var|return|using|namespace|class|struct)\b",
        RegexOptions.Compiled);
    static readonly Regex AssignmentShape = new(@"^[A-Za-z_][A-Za-z0-9_.\[\]]*\s*=\s*[^=;]+;?\s*$", RegexOptions.Compiled);

    public string Id => "CMT01";
    public string Title => "Possible commented-out code";
    public string AgentsMdReference => "§1.6 Key Anti-Pattern Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var trivia in file.Root.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                continue;
            }

            string raw = trivia.ToString();
            string content = raw.Length >= 2 ? raw[2..].Trim() : "";
            if (content.Length == 0)
            {
                continue;
            }

            if (LooksLikeCode(content))
            {
                yield return new Finding(
                    Id,
                    Severity.Suggestion,
                    file.RelativePath,
                    file.LineOfTrivia(trivia),
                    $"Comment resembles commented-out code — confirm and delete if so: {content}"
                );
            }
        }
    }

    static bool LooksLikeCode(string text) =>
        EndsWithSemicolon.IsMatch(text)
        || MethodCallShape.IsMatch(text)
        || CodeKeywordStart.IsMatch(text)
        || AssignmentShape.IsMatch(text);
}
