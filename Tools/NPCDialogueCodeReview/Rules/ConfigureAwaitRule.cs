using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>
/// AGENTS.md §1.3: avoid ConfigureAwait(false) — Unity needs the main thread,
/// and this is especially important for WebGL builds.
/// </summary>
public sealed class ConfigureAwaitRule : IReviewRule
{
    public string Id => "ASY01";
    public string Title => "Avoid ConfigureAwait(false)";
    public string AgentsMdReference => "§1.3 Async Pattern";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var invocation in file.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                memberAccess.Name.Identifier.Text != "ConfigureAwait")
            {
                continue;
            }

            yield return new Finding(
                Id,
                Severity.Warning,
                file.RelativePath,
                file.LineOf(invocation),
                "ConfigureAwait(...) call found — Unity requires continuations on the main thread; " +
                "remove it (this is especially load-bearing for WebGL builds)."
            );
        }
    }
}
