using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>AGENTS.md §1.4: line endings must be LF, not CRLF.</summary>
public sealed class CrlfLineEndingRule : IReviewRule
{
    public string Id => "FMT04";
    public string Title => "CRLF line endings (LF required)";
    public string AgentsMdReference => "§1.4 Formatting Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        if (file.RawText.Contains("\r\n"))
        {
            yield return new Finding(
                Id,
                Severity.Warning,
                file.RelativePath,
                1,
                "File contains CRLF line endings — convert to LF."
            );
        }
    }
}
