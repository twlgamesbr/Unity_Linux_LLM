using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>
/// AGENTS.md §1.6: no single-letter variables outside `for` loop counters.
/// `foreach` iterators are treated as loop counters too (same spirit).
/// </summary>
public sealed class SingleLetterVariableRule : IReviewRule
{
    public string Id => "VAR01";
    public string Title => "No single-letter variables outside loop counters";
    public string AgentsMdReference => "§1.6 Key Anti-Pattern Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        var exemptSpans = new HashSet<TextSpan>();

        foreach (var forStmt in file.Root.DescendantNodes().OfType<ForStatementSyntax>())
        {
            if (forStmt.Declaration is not null)
            {
                foreach (var variable in forStmt.Declaration.Variables)
                {
                    exemptSpans.Add(variable.Identifier.Span);
                }
            }
        }

        foreach (var foreachStmt in file.Root.DescendantNodes().OfType<ForEachStatementSyntax>())
        {
            exemptSpans.Add(foreachStmt.Identifier.Span);
        }

        foreach (var localDecl in file.Root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
        {
            foreach (var variable in localDecl.Declaration.Variables)
            {
                if (!exemptSpans.Contains(variable.Identifier.Span) && NamingUtils.IsSingleLetter(variable.Identifier.Text))
                {
                    yield return Violation(file, variable, variable.Identifier.Text);
                }
            }
        }

        foreach (var parameter in file.Root.DescendantNodes().OfType<ParameterSyntax>())
        {
            if (NamingUtils.IsSingleLetter(parameter.Identifier.Text))
            {
                yield return Violation(file, parameter, parameter.Identifier.Text);
            }
        }

        foreach (var catchDecl in file.Root.DescendantNodes().OfType<CatchDeclarationSyntax>())
        {
            if (NamingUtils.IsSingleLetter(catchDecl.Identifier.Text))
            {
                yield return Violation(file, catchDecl, catchDecl.Identifier.Text);
            }
        }
    }

    Finding Violation(SourceFile file, SyntaxNode node, string name) =>
        new(
            Id,
            Severity.Suggestion,
            file.RelativePath,
            file.LineOf(node),
            $"Single-letter identifier '{name}' outside a for/foreach loop counter — use a descriptive name."
        );
}
