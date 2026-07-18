using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>
/// AGENTS.md §2.3 / §10 (CS0108): a user-defined SendMessage(...) method on a
/// MonoBehaviour-derived class hides Component.SendMessage(string). Known,
/// documented, non-blocking warning — surfaced here so it isn't lost track of.
/// </summary>
public sealed class SendMessageHidingRule : IReviewRule
{
    public string Id => "SND01";
    public string Title => "User-defined SendMessage hides Component.SendMessage";
    public string AgentsMdReference => "§2.3 Namespace Hierarchy note / §11 Known Compile Warnings (CS0108)";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (method.Identifier.Text != "SendMessage")
            {
                continue;
            }

            bool hasNewModifier = method.Modifiers.Any(m => m.IsKind(SyntaxKind.NewKeyword));

            yield return new Finding(
                Id,
                Severity.Warning,
                file.RelativePath,
                file.LineOf(method),
                hasNewModifier
                    ? "SendMessage(...) explicitly marked 'new' — confirm callers intend the override, not Component.SendMessage."
                    : "SendMessage(...) hides Component.SendMessage(string) (CS0108) — rename this method or add 'new', " +
                      "per the documented, deliberately-deferred cleanup."
            );
        }
    }
}
