using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>AGENTS.md §1.1: method/constructor/lambda parameters must be camelCase.</summary>
public sealed class ParameterNamingRule : IReviewRule
{
    public string Id => "NAM05";
    public string Title => "Parameter naming (camelCase)";
    public string AgentsMdReference => "§1.1 Naming Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var parameter in file.Root.DescendantNodes().OfType<ParameterSyntax>())
        {
            string name = parameter.Identifier.Text;
            if (name.Length == 0 || NamingUtils.IsDiscardOrExempt(name))
            {
                continue;
            }

            if (!NamingUtils.IsCamelCase(name))
            {
                yield return new Finding(
                    Id,
                    Severity.Suggestion,
                    file.RelativePath,
                    file.LineOf(parameter),
                    $"Parameter '{name}' should be camelCase."
                );
            }
        }
    }
}
