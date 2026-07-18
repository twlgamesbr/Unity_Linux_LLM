using System.Text.RegularExpressions;

namespace NPCDialogueCodeReview.Core;

/// <summary>
/// Cross-checks every rule's <see cref="IReviewRule.AgentsMdReference"/> against
/// the live AGENTS.md headings, so a future AGENTS.md renumbering/edit that
/// breaks a rule's citation is caught automatically instead of silently
/// drifting (exactly the kind of staleness this tool exists to prevent).
/// </summary>
public static class DocsVerifier
{
    static readonly Regex SectionToken = new(@"§(\d+(?:\.\d+)?)", RegexOptions.Compiled);
    static readonly Regex HeadingLine = new(@"^#{1,6}\s+(\d+(?:\.\d+)?)\b", RegexOptions.Compiled | RegexOptions.Multiline);

    public static int Run(string agentsMdPath, IReadOnlyList<IReviewRule> rules)
    {
        if (!File.Exists(agentsMdPath))
        {
            Console.Error.WriteLine($"AGENTS.md not found at '{agentsMdPath}'. Pass --agents-md <path> if it lives elsewhere.");
            return 2;
        }

        string text = File.ReadAllText(agentsMdPath);
        var headingNumbers = HeadingLine.Matches(text).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        int missing = 0;
        int checkedCount = 0;

        foreach (var rule in rules)
        {
            var tokens = SectionToken.Matches(rule.AgentsMdReference).Select(m => m.Groups[1].Value).Distinct().ToList();
            if (tokens.Count == 0)
            {
                Console.WriteLine($"SKIP  [{rule.Id}] no §-section token found in reference text: \"{rule.AgentsMdReference}\"");
                continue;
            }

            foreach (string token in tokens)
            {
                checkedCount++;
                if (headingNumbers.Contains(token))
                {
                    Console.WriteLine($"OK    [{rule.Id}] §{token} resolves to a heading in {agentsMdPath}");
                }
                else
                {
                    Console.WriteLine($"STALE [{rule.Id}] §{token} does NOT resolve to any heading in {agentsMdPath} " +
                                       $"(reference text: \"{rule.AgentsMdReference}\") — AGENTS.md was likely renumbered/edited.");
                    missing++;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Docs verification: {checkedCount - missing} resolved, {missing} stale, {checkedCount} section references checked " +
                           $"across {rules.Count} rules.");

        return missing == 0 ? 0 : 1;
    }
}
