using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>
/// AGENTS.md §1.6: no boolean flag parameters on methods — split into named
/// methods instead. Reported at Suggestion severity because some bool
/// parameters are unavoidable (e.g. UnityEvent&lt;bool&gt; callback signatures
/// like Toggle.onValueChanged) — a human should confirm before refactoring.
/// </summary>
public sealed class BooleanParameterRule : IReviewRule
{
    public string Id => "API01";
    public string Title => "No boolean flag parameters";
    public string AgentsMdReference => "§1.6 Key Anti-Pattern Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var boolParams = method.ParameterList.Parameters
                .Where(IsBoolType)
                .Select(p => p.Identifier.Text)
                .ToList();

            if (boolParams.Count == 0)
            {
                continue;
            }

            yield return new Finding(
                Id,
                Severity.Suggestion,
                file.RelativePath,
                file.LineOf(method),
                $"Method '{method.Identifier.Text}' takes boolean flag parameter(s) [{string.Join(", ", boolParams)}] — " +
                "consider splitting into named methods, unless this signature is fixed by a delegate/event contract " +
                "(e.g. UnityEvent<bool>)."
            );
        }
    }

    static bool IsBoolType(ParameterSyntax parameter)
    {
        string typeText = parameter.Type?.ToString().TrimEnd('?') ?? "";
        return typeText is "bool" or "Boolean" or "System.Boolean";
    }
}
