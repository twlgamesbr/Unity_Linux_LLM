using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>AGENTS.md §1.1: local variables must be camelCase.</summary>
public sealed class LocalVariableNamingRule : IReviewRule
{
    public string Id => "NAM06";
    public string Title => "Local variable naming (camelCase)";
    public string AgentsMdReference => "§1.1 Naming Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var localDecl in file.Root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
        {
            if (localDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
            {
                continue; // Handled by ConstantNamingRule.
            }

            foreach (var variable in localDecl.Declaration.Variables)
            {
                string name = variable.Identifier.Text;
                if (!NamingUtils.IsDiscardOrExempt(name) && !NamingUtils.IsCamelCase(name))
                {
                    yield return Violation(file, variable, name);
                }
            }
        }

        foreach (var foreachStmt in file.Root.DescendantNodes().OfType<ForEachStatementSyntax>())
        {
            string name = foreachStmt.Identifier.Text;
            if (!NamingUtils.IsDiscardOrExempt(name) && !NamingUtils.IsCamelCase(name))
            {
                yield return Violation(file, foreachStmt, name);
            }
        }

        foreach (var catchDecl in file.Root.DescendantNodes().OfType<CatchDeclarationSyntax>())
        {
            if (catchDecl.Identifier.IsKind(SyntaxKind.None))
            {
                continue; // `catch (Exception)` with no variable.
            }

            string name = catchDecl.Identifier.Text;
            if (!NamingUtils.IsDiscardOrExempt(name) && !NamingUtils.IsCamelCase(name))
            {
                yield return Violation(file, catchDecl, name);
            }
        }
    }

    Finding Violation(SourceFile file, Microsoft.CodeAnalysis.SyntaxNode node, string name) =>
        new(
            Id,
            Severity.Suggestion,
            file.RelativePath,
            file.LineOf(node),
            $"Local variable '{name}' should be camelCase."
        );
}
