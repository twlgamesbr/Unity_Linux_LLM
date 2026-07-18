using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>
/// AGENTS.md §1.1: public/internal fields, properties, and events must be
/// PascalCase. (Public fields are discouraged elsewhere in the doc, but if
/// present they still must follow PascalCase.)
/// </summary>
public sealed class PublicMemberNamingRule : IReviewRule
{
    public string Id => "NAM02";
    public string Title => "Public/internal field, property, and event naming (PascalCase)";
    public string AgentsMdReference => "§1.1 Naming Rules";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var fieldDecl in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (!IsPublicOrInternal(fieldDecl.Modifiers) || IsConst(fieldDecl.Modifiers))
            {
                continue;
            }

            foreach (var variable in fieldDecl.Declaration.Variables)
            {
                if (!NamingUtils.IsPascalCase(variable.Identifier.Text))
                {
                    yield return Violation(file, variable, "field", variable.Identifier.Text);
                }
            }
        }

        foreach (var propertyDecl in file.Root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (!IsPublicOrInternal(propertyDecl.Modifiers))
            {
                continue;
            }

            if (!NamingUtils.IsPascalCase(propertyDecl.Identifier.Text))
            {
                yield return Violation(file, propertyDecl, "property", propertyDecl.Identifier.Text);
            }
        }

        foreach (var eventDecl in file.Root.DescendantNodes().OfType<EventFieldDeclarationSyntax>())
        {
            if (!IsPublicOrInternal(eventDecl.Modifiers))
            {
                continue;
            }

            foreach (var variable in eventDecl.Declaration.Variables)
            {
                if (!NamingUtils.IsPascalCase(variable.Identifier.Text))
                {
                    yield return Violation(file, variable, "event", variable.Identifier.Text);
                }
            }
        }
    }

    static bool IsPublicOrInternal(SyntaxTokenList modifiers) =>
        modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword) || m.IsKind(SyntaxKind.InternalKeyword));

    static bool IsConst(SyntaxTokenList modifiers) =>
        modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword));

    Finding Violation(SourceFile file, SyntaxNode node, string kind, string name) =>
        new(
            Id,
            Severity.Warning,
            file.RelativePath,
            file.LineOf(node),
            $"Public/internal {kind} '{name}' should be PascalCase."
        );
}
