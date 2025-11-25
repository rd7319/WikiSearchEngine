using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Utils
{
    public class Tokenizer
    {
        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "i","me","my","myself","we","our","ours","ourselves","you","your","yours",
            "yourself","yourselves","he","him","his","himself","she","her","hers","herself",
            "it","its","itself","they","them","their","theirs","themselves","what","which",
            "who","whom","this","that","these","those","am","is","are","was","were","be",
            "been","being","have","has","had","having","do","does","did","doing","a","an",
            "the","and","but","if","or","because","as","until","while","of","at","by","for",
            "with","about","against","between","into","through","during","before","after",
            "above","below","to","from","up","down","in","out","on","off","over","under",
            "again","further","then","once","here","there","when","where","why","how","all",
            "any","both","each","few","more","most","other","some","such","no","nor","not",
            "only","own","same","so","than","too","very","s","t","can","will","just","don",
            "should","now"
        };

        private static readonly Regex TokenSplitRegex = new(@"\W+", RegexOptions.Compiled);
        private static readonly Regex RedirectRegex = new(@"<redirect\s+title\s*=\s*""[^""]+""\s*/>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex StartsWithNumberRegex = new(@"^\d", RegexOptions.Compiled);
        public static IEnumerable<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            string normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            PorterStemmer stemmer = new PorterStemmer();
            string clean = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
            foreach (var token in TokenSplitRegex.Split(clean))
            {
                if (string.IsNullOrWhiteSpace(token)) continue;
                string cleaned = Regex.Replace(token, @"[^a-z0-9]", "");
                string w = stemmer.Stem(cleaned);
                if (string.IsNullOrEmpty(w)) continue;
                if (StartsWithNumberRegex.IsMatch(w)) continue;
                if (cleaned.Length <= 1) continue;
                if (StopWords.Contains(w)) continue;
                yield return w;
            }
        }
    }
}
