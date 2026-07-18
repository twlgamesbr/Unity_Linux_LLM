using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>
/// AGENTS.md §1.2: every [SerializeField] private field converted from an old
/// public PascalCase field should carry a matching [FormerlySerializedAs]
/// attribute so scene/prefab serialization isn't lost. This is a heuristic —
/// brand-new fields that were never public don't need it — so it reports at
/// Suggestion severity for a human to confirm.
/// </summary>
public sealed class SerializeFieldPatternRule : IReviewRule
{
    public string Id => "SER01";
    public string Title => "[SerializeField] should pair with [FormerlySerializedAs]";
    public string AgentsMdReference => "§1.2 Phase 4 [SerializeField] private Pattern";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var fieldDecl in file.Root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (!HasAttribute(fieldDecl.AttributeLists, "SerializeField"))
            {
                continue;
            }

            if (HasAttribute(fieldDecl.AttributeLists, "FormerlySerializedAs"))
            {
                continue;
            }

            foreach (var variable in fieldDecl.Declaration.Variables)
            {
                yield return new Finding(
                    Id,
                    Severity.Suggestion,
                    file.RelativePath,
                    file.LineOf(fieldDecl),
                    $"[SerializeField] field '{variable.Identifier.Text}' has no [FormerlySerializedAs] — " +
                    "confirm this field was never publicly serialized under an old PascalCase name."
                );
            }
        }
    }

    static bool HasAttribute(Microsoft.CodeAnalysis.SyntaxList<AttributeListSyntax> lists, string attributeName)
    {
        foreach (var list in lists)
        {
            foreach (var attr in list.Attributes)
            {
                string name = attr.Name.ToString();
                string shortName = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
                if (shortName == attributeName || shortName == attributeName + "Attribute")
                {
                    return true;
                }
            }
        }

        return false;
    }
}
