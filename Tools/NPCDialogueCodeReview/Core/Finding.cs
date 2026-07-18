namespace NPCDialogueCodeReview.Core;

/// <summary>
/// A single rule violation (or informational note) produced by a review rule.
/// </summary>
public sealed record Finding(
    string RuleId,
    Severity Severity,
    string RelativePath,
    int Line,
    string Message,
    string? Snippet = null
)
{
    public override string ToString() =>
        $"[{Severity}] {RuleId} {RelativePath}:{Line} — {Message}";
}
