using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NPCSystem
{
    public static class NPCSparseVectorEncoder
    {
        public static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "is", "are", "was", "were", "be", "been",
            "being", "have", "has", "had", "do", "does", "did", "will",
            "would", "could", "should", "may", "might", "shall", "can",
            "to", "of", "in", "for", "on", "with", "at", "by", "from",
            "as", "into", "through", "during", "before", "after", "above",
            "below", "between", "out", "off", "over", "under", "again",
            "further", "then", "once", "here", "there", "when", "where",
            "why", "how", "all", "each", "every", "both", "few", "more",
            "most", "other", "some", "such", "no", "nor", "not", "only",
            "own", "same", "so", "than", "too", "very", "just", "because",
            "but", "and", "or", "if", "while", "that", "this", "these",
            "those", "it", "its", "he", "she", "they", "them", "their",
            "get", "set", "has", "had", "got", "let", "put", "use", "using",
            "new", "old", "one", "two", "three", "also", "well", "back",
            "make", "made", "takes", "took", "taken", "called", "calls",
            "see", "seen", "saw", "look", "looks", "looking", "like",
            "want", "needs", "need", "tried", "try", "trying", "done",
            "going", "go", "goes", "went", "come", "came", "coming",
            "know", "knows", "known", "take", "takes", "took", "taken",
            "say", "says", "said", "think", "thinks", "thought",
            "public", "private", "protected", "internal", "static",
            "virtual", "override", "abstract", "sealed", "partial",
            "readonly", "async", "await", "void", "int", "string", "bool",
            "float", "double", "long", "char", "byte", "short", "var",
            "null", "true", "false", "this", "base", "return", "throw",
            "new", "typeof", "nameof", "sizeof", "class", "struct",
            "interface", "enum", "record", "delegate", "event", "namespace",
            "using", "import", "from", "where", "select",
            "get", "set", "value", "init", "add", "remove",
        };

        private static readonly Regex TokenRegex = new Regex(@"\b[a-zA-Z_][a-zA-Z0-9_]*\b", RegexOptions.Compiled);

        public static SparseVector Encode(string text, int maxIndex = 2_000_000)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new SparseVector { indices = new[] { 0 }, values = new[] { 0f } };

            var matches = TokenRegex.Matches(text);
            var tokens = new List<string>();
            foreach (Match match in matches)
            {
                string t = match.Value.ToLower();
                if (t.Length > 1 && !StopWords.Contains(t))
                {
                    tokens.Add(t);
                }
            }

            if (tokens.Count == 0)
                return new SparseVector { indices = new[] { 0 }, values = new[] { 0f } };

            var freq = new Dictionary<string, int>();
            foreach (var t in tokens)
            {
                if (freq.ContainsKey(t)) freq[t]++;
                else freq[t] = 1;
            }

            float maxFreq = freq.Values.Max();
            var kv = new Dictionary<int, float>();

            using (var sha256 = SHA256.Create())
            {
                foreach (var entry in freq)
                {
                    int idx = GetTokenIndex(entry.Key, sha256, maxIndex);
                    float val = entry.Value / maxFreq;
                    if (kv.ContainsKey(idx)) kv[idx] += val;
                    else kv[idx] = val;
                }
            }

            var sortedIndices = kv.Keys.OrderBy(k => k).ToArray();
            var values = sortedIndices.Select(i => kv[i]).ToArray();

            return new SparseVector { indices = sortedIndices, values = values };
        }

        private static int GetTokenIndex(string token, SHA256 sha256, int maxIndex)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(token);
            byte[] hash = sha256.ComputeHash(bytes);
            
            // Porting Python: h = hashlib.sha256(token.encode("utf-8")).hexdigest()[:8]
            // return int(h, 16) % max_index
            
            // First 4 bytes (8 hex chars)
            // Big-endian interpretation to match hex string prefix
            uint val = (uint)hash[0] << 24 | (uint)hash[1] << 16 | (uint)hash[2] << 8 | (uint)hash[3];
            return (int)(val % (uint)maxIndex);
        }
    }

    [Serializable]
    public class SparseVector
    {
        public int[] indices;
        public float[] values;
    }
}
