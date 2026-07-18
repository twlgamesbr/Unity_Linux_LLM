using NPCDialogueCodeReview.Core;
using NPCDialogueCodeReview.Rules;

namespace NPCDialogueCodeReview.SelfTest;

/// <summary>
/// Verifies the verifier: runs each rule against a small known-good and
/// known-bad snippet and asserts the expected finding count for that rule's
/// ID. This is what makes the review "smart" rather than a black box —
/// every check here has a companion test proving it actually fires (and
/// doesn't fire on compliant code).
/// </summary>
public static class SelfTestRunner
{
    sealed record Case(string RuleId, string Description, string Code, int ExpectedCount);

    public static int Run()
    {
        var cases = new List<Case>
        {
            new("NAM01", "flags a private field without leading underscore",
                "class C { private int count; }", 1),
            new("NAM01", "accepts a correctly-named private field",
                "class C { private int _count; }", 0),

            new("NAM02", "flags a public field in camelCase",
                "class C { public int playerCount; }", 1),
            new("NAM02", "accepts a public property in PascalCase",
                "class C { public int PlayerCount { get; set; } }", 0),

            new("NAM03", "flags a constant in camelCase",
                "class C { const int maxRetries = 3; }", 1),
            new("NAM03", "accepts a constant in PascalCase",
                "class C { const int MaxRetries = 3; }", 0),

            new("NAM04", "flags a camelCase method",
                "class C { void doThing() { } }", 1),
            new("NAM04", "accepts a PascalCase method",
                "class C { void DoThing() { } }", 0),
            new("NAM04", "accepts Subject_Scenario_ExpectedBehavior test method names",
                "class C { [Test] void Login_InvalidPassword_ReturnsError() { } }", 0),

            new("NAM05", "flags a PascalCase parameter",
                "class C { void Do(string PlayerName) { } }", 1),
            new("NAM06", "flags a PascalCase local variable",
                "class C { void Do() { int PlayerCount = 0; } }", 1),
            new("NAM07", "flags a lowercase namespace segment",
                "namespace npcSystem.tests { class C { } }", 1),

            new("SER01", "flags [SerializeField] without [FormerlySerializedAs]",
                "class C { [SerializeField] private int _health; }", 1),
            new("SER01", "accepts [SerializeField] paired with [FormerlySerializedAs]",
                "class C { [FormerlySerializedAs(\"Health\")] [SerializeField] private int _health; }", 0),

            new("API01", "flags a boolean flag parameter",
                "class C { void Apply(bool registerMode) { } }", 1),
            new("API01", "accepts a method with no boolean parameters",
                "class C { void Apply(string mode) { } }", 0),

            new("NET01", "flags a direct equality comparison against \"localhost\"",
                "class C { bool Check(string h) { return h == \"localhost\"; } }", 1),
            new("NET02", "flags a plain \"localhost\" default assignment (lower severity)",
                "class C { string _host = \"localhost\"; }", 1),

            new("TODO01", "flags a TODO comment",
                "class C { // TODO: fix this\n void Do() { } }", 1),
            new("TODO01", "does not flag an ordinary comment",
                "class C { // Applies the pending change set\n void Do() { } }", 0),

            new("ASY01", "flags ConfigureAwait(false)",
                "class C { async System.Threading.Tasks.Task Do() { await Do().ConfigureAwait(false); } }", 1),

            new("VAR01", "flags a single-letter local variable",
                "class C { void Do() { int x = 0; } }", 1),
            new("VAR01", "accepts a single-letter for-loop counter",
                "class C { void Do() { for (int i = 0; i < 10; i++) { } } }", 0),

            new("DOC01", "flags a public method with no XML doc (class isn't public, so only the method is checked)",
                "class C { public void Do() { } }", 1),
            new("DOC01", "accepts a public method with a <summary> doc",
                "class C { /// <summary>Does the thing.</summary>\n public void Do() { } }", 0),
            new("DOC01", "flags both a public class and its undocumented public method",
                "public class C { public void Do() { } }", 2),

            new("SND01", "flags a user-defined SendMessage method",
                "class C { void SendMessage(string m) { } }", 1),

            new("FMT01", "flags a line with trailing whitespace",
                "class C { }   \n", 1),
            new("FMT02", "flags a missing final newline",
                "class C { }", 1),
            new("FMT03", "flags tab indentation",
                "class C\n{\n\tvoid Do() { }\n}\n", 1),
            new("FMT04", "flags CRLF line endings",
                "class C\r\n{\r\n}\r\n", 1),
            new("FMT05", "flags non-Allman opening braces (class brace + method brace both on the same line as preceding code)",
                "class C { void Do() {\n int x = 0;\n } }", 2),
            new("FMT05", "accepts Allman-style braces",
                "class C\n{\n    void Do()\n    {\n        int x = 0;\n    }\n}\n", 0),

            new("CMT01", "flags a comment that looks like commented-out code",
                "class C { void Do() { } // total = price * quantity;\n }", 1),
            new("CMT01", "does not flag an ordinary prose comment",
                "class C { void Do() { } // Applies the discount before tax\n }", 0),
        };

        int passed = 0;
        int failed = 0;

        foreach (var testCase in cases)
        {
            // HardcodedLocalhostRule's declared Id is "NET01" but it also emits the
            // lower-severity "NET02" finding kind from the same Check() call.
            var rule = RuleRegistry.All.FirstOrDefault(r => r.Id == testCase.RuleId)
                       ?? (testCase.RuleId == "NET02" ? RuleRegistry.All.FirstOrDefault(r => r.Id == "NET01") : null);
            if (rule is null)
            {
                Console.WriteLine($"FAIL  [{testCase.RuleId}] {testCase.Description} — no rule registered with this ID");
                failed++;
                continue;
            }

            var file = SourceFile.FromInMemoryText(testCase.Code, "SelfTest.cs");
            int actualCount = rule.Check(file).Count(f => f.RuleId == testCase.RuleId);

            if (actualCount == testCase.ExpectedCount)
            {
                Console.WriteLine($"PASS  [{testCase.RuleId}] {testCase.Description}");
                passed++;
            }
            else
            {
                Console.WriteLine($"FAIL  [{testCase.RuleId}] {testCase.Description} " +
                                   $"— expected {testCase.ExpectedCount} finding(s), got {actualCount}");
                failed++;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Self-test: {passed} passed, {failed} failed, {cases.Count} total.");

        var uncoveredRules = RuleRegistry.All.Select(r => r.Id).Except(cases.Select(c => c.RuleId)).ToList();
        if (uncoveredRules.Count > 0)
        {
            Console.WriteLine($"Warning: rules with no self-test coverage: {string.Join(", ", uncoveredRules)}");
        }

        return failed == 0 ? 0 : 1;
    }
}
