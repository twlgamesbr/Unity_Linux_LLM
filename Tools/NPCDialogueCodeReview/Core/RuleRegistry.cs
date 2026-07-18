using NPCDialogueCodeReview.Rules;

namespace NPCDialogueCodeReview.Core;

/// <summary>Central list of every rule the review runs. Add new rules here.</summary>
public static class RuleRegistry
{
    public static IReadOnlyList<IReviewRule> All { get; } = new List<IReviewRule>
    {
        new PrivateFieldNamingRule(),
        new PublicMemberNamingRule(),
        new ConstantNamingRule(),
        new MethodNamingRule(),
        new ParameterNamingRule(),
        new LocalVariableNamingRule(),
        new NamespaceNamingRule(),
        new SerializeFieldPatternRule(),
        new BooleanParameterRule(),
        new HardcodedLocalhostRule(),
        new TodoFixmeHackRule(),
        new CommentedOutCodeRule(),
        new ConfigureAwaitRule(),
        new SingleLetterVariableRule(),
        new XmlDocRule(),
        new SendMessageHidingRule(),
        new TrailingWhitespaceRule(),
        new FinalNewlineRule(),
        new TabIndentationRule(),
        new CrlfLineEndingRule(),
        new AllmanBraceRule(),
    };
}
