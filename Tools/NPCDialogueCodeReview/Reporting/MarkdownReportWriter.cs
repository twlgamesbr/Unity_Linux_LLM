using System.Text;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Reporting;

public static class MarkdownReportWriter
{
    public static string Write(ReviewReport report, IReadOnlyList<IReviewRule> rules)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# NPCDialogue Automated Code Review");
        sb.AppendLine();
        sb.AppendLine($"Generated: {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("Scanned paths:");
        foreach (string path in report.ScannedPaths)
        {
            sb.AppendLine($"- `{path}`");
        }
        sb.AppendLine();
        sb.AppendLine($"Files scanned: **{report.FilesScanned}** · Rules run: **{report.RulesRun}** · " +
                       $"Findings: **{report.Findings.Count}**");
        sb.AppendLine();
        sb.AppendLine("Tool: `Tools/NPCDialogueCodeReview` — see its README.md for usage and how to add rules.");
        sb.AppendLine();

        sb.AppendLine("## Summary by severity");
        sb.AppendLine();
        sb.AppendLine("| Severity | Count |");
        sb.AppendLine("|---|---|");
        foreach (Severity severity in Enum.GetValues<Severity>().OrderByDescending(s => s))
        {
            int count = report.CountsBySeverity.GetValueOrDefault(severity);
            sb.AppendLine($"| {severity} | {count} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Summary by rule");
        sb.AppendLine();
        sb.AppendLine("| Rule ID | Title | AGENTS.md reference | Findings |");
        sb.AppendLine("|---|---|---|---|");
        var countsByRule = report.CountsByRule;

        // Some rules emit more than one RuleId from a single Check() call (e.g.
        // HardcodedLocalhostRule's declared Id is NET01, but it also emits the
        // lower-severity NET02 kind too). Union the registry's declared IDs with
        // any extra IDs actually seen in findings so nothing is silently dropped.
        var registryIds = rules.ToDictionary(r => r.Id);
        var extraIds = countsByRule.Keys.Except(registryIds.Keys);
        var allIds = registryIds.Keys.Concat(extraIds).Distinct().OrderBy(id => id, StringComparer.Ordinal);

        foreach (string id in allIds)
        {
            int count = countsByRule.GetValueOrDefault(id);
            string title = registryIds.TryGetValue(id, out var rule)
                ? rule.Title
                : $"(sub-check of {id[..3]}01)";
            string reference = registryIds.TryGetValue(id, out var ruleForRef) ? ruleForRef.AgentsMdReference : "—";
            sb.AppendLine($"| {id} | {title} | {reference} | {count} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Findings");
        sb.AppendLine();

        if (report.Findings.Count == 0)
        {
            sb.AppendLine("No findings. 🎉");
        }
        else
        {
            foreach (var group in report.Findings.GroupBy(f => f.RelativePath).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"### `{group.Key}`");
                sb.AppendLine();
                sb.AppendLine("| Line | Severity | Rule | Message |");
                sb.AppendLine("|---|---|---|---|");
                foreach (var finding in group.OrderBy(f => f.Line))
                {
                    string message = finding.Message.Replace("|", "\\|");
                    sb.AppendLine($"| {finding.Line} | {finding.Severity} | {finding.RuleId} | {message} |");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
