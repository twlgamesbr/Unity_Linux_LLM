using System.Text.RegularExpressions;

namespace NPCDialogueCodeReview.Core;

public static class NamingUtils
{
    static readonly Regex PrivateFieldPattern = new(@"^_[a-z][a-zA-Z0-9]*$", RegexOptions.Compiled);
    static readonly Regex PascalCasePattern = new(@"^[A-Z][a-zA-Z0-9]*$", RegexOptions.Compiled);
    static readonly Regex PascalCaseWithUnderscoreSegmentsPattern =
        new(@"^[A-Z][a-zA-Z0-9]*(_[A-Z][a-zA-Z0-9]*)*$", RegexOptions.Compiled);
    static readonly Regex CamelCasePattern = new(@"^[a-z][a-zA-Z0-9]*$", RegexOptions.Compiled);

    public static bool IsPrivateFieldCase(string name) => PrivateFieldPattern.IsMatch(name);

    public static bool IsPascalCase(string name) => PascalCasePattern.IsMatch(name);

    /// <summary>
    /// PascalCase, but allows underscore-separated PascalCase segments — the
    /// documented test-naming convention: Subject_Scenario_ExpectedBehavior().
    /// </summary>
    public static bool IsPascalCaseOrTestSegments(string name) =>
        PascalCaseWithUnderscoreSegmentsPattern.IsMatch(name);

    public static bool IsCamelCase(string name) => CamelCasePattern.IsMatch(name);

    public static bool IsSingleLetter(string name) => name.Length == 1 && char.IsLetter(name[0]);

    /// <summary>Names that are conventionally exempt from single-letter checks (discards, generics).</summary>
    public static bool IsDiscardOrExempt(string name) => name is "_";
}
