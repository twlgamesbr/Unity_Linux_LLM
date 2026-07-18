using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>AGENTS.md §1.4: files must end with a final newline.</summary>
public sealed class FinalNewlineRule : IReviewRule
{
    public string Id => "FMT02";
    public string Title => "Missing final newline";
    public string AgentsMdReference => "§1.4 Formatting Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        if (file.RawText.Length == 0)
        {
            yield break;
        }

        if (!file.RawText.EndsWith('\n'))
        {
            yield return new Finding(
                Id,
                Severity.Warning,
                file.RelativePath,
                file.Lines.Length,
                "File does not end with a final newline."
            );
        }
    }
}
