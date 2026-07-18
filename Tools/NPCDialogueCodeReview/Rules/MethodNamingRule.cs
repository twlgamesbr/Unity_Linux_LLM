using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NPCDialogueCodeReview.Core;

namespace NPCDialogueCodeReview.Rules;

/// <summary>
/// AGENTS.md §1.1: methods must be PascalCase regardless of accessibility.
/// Test methods (decorated with NUnit attributes) may use the documented
/// Subject_Scenario_ExpectedBehavior underscore-segment convention (§7 Testing).
/// </summary>
public sealed class MethodNamingRule : IReviewRule
{
    static readonly HashSet<string> TestAttributeNames = new(StringComparer.Ordinal)
    {
        "Test", "TestCase", "TestCaseSource", "UnityTest", "UnitySetUp", "UnityTearDown",
        "SetUp", "TearDown", "OneTimeSetUp", "OneTimeTearDown",
    };

    public string Id => "NAM04";
    public string Title => "Method naming (PascalCase)";
    public string AgentsMdReference => "§1.1 Naming Rules / §7.2 Test Patterns";

    public IEnumerable<Finding> Check(SourceFile file)
    {
        foreach (var method in file.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            if (method.ExplicitInterfaceSpecifier is not null)
            {
                continue; // Name is dictated by the interface contract.
            }

            string name = method.Identifier.Text;
            bool isTestMethod = HasAnyAttribute(method.AttributeLists, TestAttributeNames);

            bool valid = isTestMethod
                ? NamingUtils.IsPascalCaseOrTestSegments(name)
                : NamingUtils.IsPascalCase(name);

            if (!valid)
            {
                yield return new Finding(
                    Id,
                    Severity.Warning,
                    file.RelativePath,
                    file.LineOf(method),
                    $"Method '{name}' should be PascalCase" +
                    (isTestMethod ? " (or Subject_Scenario_ExpectedBehavior segments)." : ".")
                );
            }
        }
    }

    static bool HasAnyAttribute(SyntaxList<AttributeListSyntax> lists, HashSet<string> names)
    {
        foreach (var list in lists)
        {
            foreach (var attr in list.Attributes)
            {
                string attrName = attr.Name.ToString();
                string shortName = attrName.Contains('.') ? attrName[(attrName.LastIndexOf('.') + 1)..] : attrName;
                if (names.Contains(shortName))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
