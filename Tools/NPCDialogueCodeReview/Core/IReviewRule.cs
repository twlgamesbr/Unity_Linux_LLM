namespace NPCDialogueCodeReview.Core;

/// <summary>
/// A single, independently testable code-rule check. Each rule maps to one or
/// more entries in AGENTS.md §1 (Code Conventions) and reports zero or more
/// <see cref="Finding"/> instances per file.
/// </summary>
public interface IReviewRule
{
    /// <summary>Short, stable identifier shown in reports (e.g. "NAM01").</summary>
    string Id { get; }

    /// <summary>Human-readable rule name.</summary>
    string Title { get; }

    /// <summary>The AGENTS.md section this rule enforces, for traceability.</summary>
    string AgentsMdReference { get; }

    IEnumerable<Finding> Check(SourceFile file);
}
