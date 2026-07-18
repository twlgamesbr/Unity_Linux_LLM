namespace NPCDialogueCodeReview.Core;

/// <summary>Walks a directory tree, parses every .cs file once, and runs all rules against it.</summary>
public sealed class ReviewRunner
{
    readonly IReadOnlyList<IReviewRule> _rules;

    public ReviewRunner(IReadOnlyList<IReviewRule> rules)
    {
        _rules = rules;
    }

    public List<Finding> Run(string relativePathRoot, IEnumerable<string> directories)
    {
        var findings = new List<Finding>();
        int fileCount = 0;

        foreach (string directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                Console.Error.WriteLine($"warning: path not found, skipping: {directory}");
                continue;
            }

            var files = Directory
                .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                            && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .OrderBy(f => f, StringComparer.Ordinal);

            foreach (string filePath in files)
            {
                string relativePath = Path.GetRelativePath(relativePathRoot, filePath).Replace('\\', '/');
                var sourceFile = new SourceFile(filePath, relativePath);
                fileCount++;

                foreach (var rule in _rules)
                {
                    findings.AddRange(rule.Check(sourceFile));
                }
            }
        }

        FilesScanned = fileCount;
        return findings;
    }

    public int FilesScanned { get; private set; }
}
