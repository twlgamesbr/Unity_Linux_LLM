namespace NPCDialogueCodeReview.Core;

/// <summary>
/// Confidence/impact level of a finding, ordered low → high for threshold filtering.
/// </summary>
public enum Severity
{
    Info = 0,
    Suggestion = 1,
    Warning = 2,
    Error = 3,
}
