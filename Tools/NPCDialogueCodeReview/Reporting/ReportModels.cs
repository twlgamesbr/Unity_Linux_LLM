using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Reporting;

public sealed record ReviewReport(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<string> ScannedPaths,
    int FilesScanned,
    int RulesRun,
    IReadOnlyList<Finding> Findings
)
{
    public IEnumerable<Finding> BySeverityDesc =>
        Findings.OrderByDescending(f => f.Severity).ThenBy(f => f.RelativePath).ThenBy(f => f.Line);

    public IReadOnlyDictionary<Severity, int> CountsBySeverity =>
        Findings.GroupBy(f => f.Severity).ToDictionary(g => g.Key, g => g.Count());

    public IReadOnlyDictionary<string, int> CountsByRule =>
        Findings.GroupBy(f => f.RuleId).OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count());
}
