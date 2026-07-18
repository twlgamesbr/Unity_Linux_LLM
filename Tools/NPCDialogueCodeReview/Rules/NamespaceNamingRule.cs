using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>AGENTS.md §1.1: namespaces must be PascalCase (NPCSystem.*).</summary>
public sealed class NamespaceNamingRule : IReviewRule
{
    public string Id => "NAM07";
    public string Title => "Namespace naming (PascalCase)";
    public string AgentsMdReference => "§1.1 Naming Rules / §2.3 Namespace Hierarchy";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var node in file.Root.DescendantNodes())
        {
            string? name = node switch
            {
                NamespaceDeclarationSyntax ns => ns.Name.ToString(),
                FileScopedNamespaceDeclarationSyntax fsn => fsn.Name.ToString(),
                _ => null,
            };

            if (name is null)
            {
                continue;
            }

            foreach (string segment in name.Split('.'))
            {
                if (!NamingUtils.IsPascalCase(segment))
                {
                    yield return new Finding(
                        Id,
                        Severity.Suggestion,
                        file.RelativePath,
                        file.LineOf(node),
                        $"Namespace segment '{segment}' in '{name}' should be PascalCase."
                    );
                    break;
                }
            }
        }
    }
}
