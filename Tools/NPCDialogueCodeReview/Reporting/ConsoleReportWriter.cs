using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Reporting;

public static class ConsoleReportWriter
{
    public static void Write(ReviewReport report, Severity minSeverity)
    {
        Console.WriteLine();
        Console.WriteLine("=== NPCDialogue Automated Code Review ===");
        Console.WriteLine($"Files scanned: {report.FilesScanned}   Rules run: {report.RulesRun}   " +
                           $"Total findings: {report.Findings.Count}");
        Console.WriteLine();

        foreach (Severity severity in Enum.GetValues<Severity>().OrderByDescending(s => s))
        {
            int count = report.CountsBySeverity.GetValueOrDefault(severity);
            Console.WriteLine($"  {severity,-10}: {count}");
        }

        Console.WriteLine();

        var visible = report.BySeverityDesc.Where(f => f.Severity >= minSeverity).ToList();
        if (visible.Count == 0)
        {
            Console.WriteLine($"No findings at or above {minSeverity} severity.");
            return;
        }

        Console.WriteLine($"--- Findings at or above {minSeverity} severity ({visible.Count}) ---");
        foreach (var finding in visible)
        {
            Console.WriteLine(finding.ToString());
        }
    }
}
