using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>AGENTS.md §1.4: trailing whitespace must be trimmed.</summary>
public sealed class TrailingWhitespaceRule : IReviewRule
{
    public string Id => "FMT01";
    public string Title => "Trailing whitespace";
    public string AgentsMdReference => "§1.4 Formatting Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        for (int i = 0; i < file.Lines.Length; i++)
        {
            string line = file.Lines[i].TrimEnd('\r');
            if (line.Length > 0 && (line[^1] == ' ' || line[^1] == '\t'))
            {
                yield return new Finding(
                    Id,
                    Severity.Warning,
                    file.RelativePath,
                    i + 1,
                    "Trailing whitespace on this line."
                );
            }
        }
    }
}
