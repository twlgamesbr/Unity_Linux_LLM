using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NPCDialogueCodeReview.Core;

/// <summary>
/// Parsed representation of a single .cs file, shared across all rules so the
/// text is only read and parsed once per file.
/// </summary>
public sealed class SourceFile
{
    public string FilePath { get; }
    public string RelativePath { get; }
    public string RawText { get; }
    public string[] Lines { get; }
    public SyntaxTree Tree { get; }
    public CompilationUnitSyntax Root { get; }

    public SourceFile(string filePath, string relativePath)
        : this(filePath, relativePath, File.ReadAllText(filePath))
    {
    }

    /// <summary>Used by self-tests to check rules against in-memory snippets without touching disk.</summary>
    public static SourceFile FromInMemoryText(string text, string fakeRelativePath) =>
        new(fakeRelativePath, fakeRelativePath, text);

    SourceFile(string filePath, string relativePath, string rawText)
    {
        FilePath = filePath;
        RelativePath = relativePath;
        RawText = rawText;
        Lines = RawText.Split('\n');
        Tree = CSharpSyntaxTree.ParseText(RawText, path: filePath);
        Root = Tree.GetCompilationUnitRoot();
    }

    public int LineOf(SyntaxNode node) =>
        Tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;

    public int LineOfToken(SyntaxToken token) =>
        Tree.GetLineSpan(token.Span).StartLinePosition.Line + 1;

    public int LineOfTrivia(SyntaxTrivia trivia) =>
        Tree.GetLineSpan(trivia.Span).StartLinePosition.Line + 1;

    /// <summary>Returns the raw source line (1-based) trimmed of the trailing '\r', if any.</summary>
    public string LineText(int oneBasedLine)
    {
        if (oneBasedLine < 1 || oneBasedLine > Lines.Length)
        {
            return "";
        }

        return Lines[oneBasedLine - 1].TrimEnd('\r');
    }
}
