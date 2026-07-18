using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>AGENTS.md §1.1: constants must be PascalCase, regardless of accessibility.</summary>
public sealed class ConstantNamingRule : IReviewRule
{
    public string Id => "NAM03";
    public string Title => "Constant naming (PascalCase)";
    public string AgentsMdReference => "§1.1 Naming Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var fieldDecl in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (!fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
            {
                continue;
            }

            foreach (var variable in fieldDecl.Declaration.Variables)
            {
                if (!NamingUtils.IsPascalCase(variable.Identifier.Text))
                {
                    yield return new Finding(
                        Id,
                        Severity.Warning,
                        file.RelativePath,
                        file.LineOf(variable),
                        $"Constant '{variable.Identifier.Text}' should be PascalCase."
                    );
                }
            }
        }

        foreach (var localConst in file.Root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
        {
            if (!localConst.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
            {
                continue;
            }

            foreach (var variable in localConst.Declaration.Variables)
            {
                if (!NamingUtils.IsPascalCase(variable.Identifier.Text))
                {
                    yield return new Finding(
                        Id,
                        Severity.Suggestion,
                        file.RelativePath,
                        file.LineOf(variable),
                        $"Local constant '{variable.Identifier.Text}' should be PascalCase."
                    );
                }
            }
        }
    }
}
