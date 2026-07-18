using NPCDialogueCodeReview.Core;
using NPCDialogueCodeReview.Reporting;
using NPCDialogueCodeReview.SelfTest;

var paths = new List<string>();
string format = "both";
string outPath = "Tools/NPCDialogueCodeReview/reports/npc-dialogue-review.md";
Severity failOn = Severity.Warning;
bool failOnDisabled = false;
Severity minConsoleSeverity = Severity.Suggestion;
bool runSelfTest = false;
bool listRules = false;
bool verifyDocs = false;
string relativeRoot = Directory.GetCurrentDirectory();
string agentsMdPath = "AGENTS.md";

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--path":
            paths.Add(args[++i]);
            break;
        case "--root":
            relativeRoot = args[++i];
            break;
        case "--format":
            format = args[++i];
            break;
        case "--out":
            outPath = args[++i];
            break;
        case "--fail-on":
            string failOnValue = args[++i];
            if (string.Equals(failOnValue, "none", StringComparison.OrdinalIgnoreCase))
            {
                failOnDisabled = true;
            }
            else
            {
                failOn = ParseSeverity(failOnValue, Severity.Warning);
            }
            break;
        case "--min-severity":
            minConsoleSeverity = ParseSeverity(args[++i], Severity.Suggestion);
            break;
        case "--self-test":
            runSelfTest = true;
            break;
        case "--list-rules":
            listRules = true;
            break;
        case "--verify-docs":
            verifyDocs = true;
            break;
        case "--agents-md":
            agentsMdPath = args[++i];
            break;
        case "--help":
        case "-h":
            PrintUsage();
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            PrintUsage();
            return 2;
    }
}

if (runSelfTest)
{
    return SelfTestRunner.Run();
}

if (verifyDocs)
{
    return DocsVerifier.Run(agentsMdPath, RuleRegistry.All);
}

if (listRules)
{
    foreach (var rule in RuleRegistry.All.OrderBy(r => r.Id))
    {
        Console.WriteLine($"{rule.Id,-8} {rule.Title} ({rule.AgentsMdReference})");
    }
    return 0;
}

if (paths.Count == 0)
{
    paths.Add("Assets/Scripts/Runtime/NPCDialogue");
}

var runner = new ReviewRunner(RuleRegistry.All);
var findings = runner.Run(relativeRoot, paths);

var report = new ReviewReport(
    DateTimeOffset.UtcNow,
    paths,
    runner.FilesScanned,
    RuleRegistry.All.Count,
    findings
);

if (format is "console" or "both")
{
    ConsoleReportWriter.Write(report, minConsoleSeverity);
}

if (format is "markdown" or "both")
{
    string? directory = Path.GetDirectoryName(outPath);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    File.WriteAllText(outPath, MarkdownReportWriter.Write(report, RuleRegistry.All));
    Console.WriteLine();
    Console.WriteLine($"Markdown report written to {outPath}");
}

bool shouldFail = !failOnDisabled && report.Findings.Any(f => f.Severity >= failOn);
return shouldFail ? 1 : 0;

static Severity ParseSeverity(string value, Severity fallback) =>
    Enum.TryParse<Severity>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

static void PrintUsage()
{
    Console.WriteLine("""
        NPCDialogue automated code review — scans .cs files against AGENTS.md §1 code rules.

        Usage:
          dotnet run --project Tools/NPCDialogueCodeReview -- [options]

        Options:
          --path <dir>          Directory to scan (repeatable). Default: Assets/Scripts/Runtime/NPCDialogue
          --root <dir>          Base directory relative paths are computed against. Default: current directory
          --format <mode>       console | markdown | both (default: both)
          --out <file>          Markdown report path (default: Tools/NPCDialogueCodeReview/reports/npc-dialogue-review.md)
          --fail-on <severity>  Info | Suggestion | Warning | Error | None — exit 1 if any finding meets/exceeds it (default: Warning)
          --min-severity <sev>  Minimum severity printed to console (default: Suggestion)
          --list-rules          Print the rule catalog and exit
          --self-test           Run the built-in rule self-tests and exit (verifies the verifier)
          --verify-docs         Check every rule's AGENTS.md §-reference still resolves to a real heading, then exit
          --agents-md <path>    Path to AGENTS.md for --verify-docs (default: AGENTS.md)
          --help                Show this message
        """);
}
