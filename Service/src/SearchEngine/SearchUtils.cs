using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchEngine
{
    internal class SearchUtils
    {
        internal static double CalculateSectionBoost(int sectionFlags)
        {
            double boost = 1.0;

            if ((sectionFlags & 32) != 0) boost *= 3.0;     // title
            if ((sectionFlags & 16) != 0) boost *= 1.6;     // category
            if ((sectionFlags & 8) != 0) boost *= 1.1;     // body
            if ((sectionFlags & 4) != 0) boost *= 1.05;    // link
            if ((sectionFlags & 2) != 0) boost *= 1.2;     // infobox
            if ((sectionFlags & 1) != 0) boost *= 1.1;     // geobox

            return boost;
        }

        internal static List<(string docId, int tf, double offsetScore, List<int> positions)> ParsePosting(List<byte[]> data)
        {
            var results = new List<(string, int, double, List<int>)>();

            foreach (var line in data)
            {
                var s = Encoding.UTF8.GetString(line);
                foreach (var item in s.Split(','))
                {
                    if (string.IsNullOrWhiteSpace(item)) continue;

                    var parts = item.Split(':');
                    var info = parts[0].Split('$');
                    string docId = info[0];
                    int sectionOffset = int.Parse(info[1]);

                    double offsetScore = CalculateSectionBoost(sectionOffset);

                    var freqArr = parts[1].Split('|');
                    int tf = int.Parse(freqArr[0]);

                    List<int> posList = freqArr[1].Split('-')
                        .Select(int.Parse)
                        .ToList();

                    results.Add((docId, tf, offsetScore, posList));
                }
            }
            return results;
        }
        
        internal static double TitleBoost(string title, string query)
        {
            title = title.ToLower();
            query = query.ToLower();

            if (title == query)
                return 1000.0;                    // Exact title match → HUGE boost

            if (title.StartsWith(query))
                return 500.0;                     // Prefix match

            if (title.Contains(query))
                return 300.0;                     // Substring match

            // Word-level match
            var titleWords = title.Split(' ');
            var queryWords = query.Split(' ');
            int wordFactor = 0;
            foreach (var word in queryWords)
            {
                if (titleWords.Contains(word))
                    //return 20.0;
                    wordFactor += 10;
            }
            if (wordFactor > 0) return wordFactor;
            return 1.0;
        }

        internal static double ComputePhraseScoreInOrder(List<string> orderedTerms, Dictionary<string, List<int>> termPositions)
        {
            if (orderedTerms.Count <= 1)
                return 0;

            double score = 0;

            for (int i = 0; i < orderedTerms.Count - 1; i++)
            {
                var posA = termPositions[orderedTerms[i]];
                var posB = termPositions[orderedTerms[i + 1]];

                foreach (var a in posA)
                {
                    foreach (var b in posB)
                    {
                        int diff = b - a;

                        if (diff == 1)
                            score += 10;   // exact phrase match
                        else if (diff == 2)
                            score += 8;
                        else if (diff > 2 && diff <= 4)
                            score += 4;    // near-phrase
                        else if (diff > 3 && diff <= 6)
                            score += 1;    // weak proximity
                        else break;
                    }
                }
            }

            return score;
        }
    }
}
