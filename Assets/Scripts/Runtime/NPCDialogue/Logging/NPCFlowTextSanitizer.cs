using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace NPCSystem
{
    public static class NPCFlowTextSanitizer
    {
        public static Dictionary<string, object> SummarizeText(string text, bool includeSnippet, int maxSnippetChars)
        {
            string safeText = text ?? string.Empty;
            var summary = new Dictionary<string, object>
            {
                ["length"] = safeText.Length,
                ["sha256"] = Sha256Hex(safeText)
            };

            if (includeSnippet)
            {
                string snippet = safeText.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
                int limit = Math.Max(0, maxSnippetChars);
                if (snippet.Length > limit)
                {
                    snippet = snippet.Substring(0, limit);
                }
                summary["snippet"] = snippet;
            }

            return summary;
        }

        public static Dictionary<string, object> MergeSummary(Dictionary<string, object> target, string prefix, string text, bool includeSnippet, int maxSnippetChars)
        {
            target ??= new Dictionary<string, object>();
            Dictionary<string, object> summary = SummarizeText(text, includeSnippet, maxSnippetChars);
            foreach (KeyValuePair<string, object> pair in summary)
            {
                target[$"{prefix}{pair.Key}"] = pair.Value;
            }
            return target;
        }

        static string Sha256Hex(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                StringBuilder builder = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
