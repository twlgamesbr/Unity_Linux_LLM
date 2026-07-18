using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>AGENTS.md §1.4: indentation must be spaces (4 per level), not tabs.</summary>
public sealed class TabIndentationRule : IReviewRule
{
    public string Id => "FMT03";
    public string Title => "Tab indentation (spaces required)";
    public string AgentsMdReference => "§1.4 Formatting Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        for (int i = 0; i < file.Lines.Length; i++)
        {
            string line = file.Lines[i];
            int leadingLength = 0;
            while (leadingLength < line.Length && (line[leadingLength] == ' ' || line[leadingLength] == '\t'))
            {
                leadingLength++;
            }

            if (line[..leadingLength].Contains('\t'))
            {
                yield return new Finding(
                    Id,
                    Severity.Warning,
                    file.RelativePath,
                    i + 1,
                    "Line is indented with a tab character — use 4 spaces per level."
                );
            }
        }
    }
}
