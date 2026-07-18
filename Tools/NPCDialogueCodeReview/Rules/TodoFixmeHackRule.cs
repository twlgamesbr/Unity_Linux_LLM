using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>AGENTS.md §1.6: no TODO/FIXME/HACK markers — address or remove.</summary>
public sealed class TodoFixmeHackRule : IReviewRule
{
    static readonly Regex Marker = new(@"\b(TODO|FIXME|HACK)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Id => "TODO01";
    public string Title => "No TODO/FIXME/HACK markers";
    public string AgentsMdReference => "§1.6 Key Anti-Pattern Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var trivia in file.Root.DescendantTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) &&
                !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) &&
                !trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) &&
                !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                continue;
            }

            string text = trivia.ToString();
            var match = Marker.Match(text);
            if (!match.Success)
            {
                continue;
            }

            yield return new Finding(
                Id,
                Severity.Warning,
                file.RelativePath,
                file.LineOfTrivia(trivia),
                $"Comment contains '{match.Value.ToUpperInvariant()}' marker — address or remove: " +
                text.Trim().Replace('\n', ' ')
            );
        }
    }
}
