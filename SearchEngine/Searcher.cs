using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using WikiIndexBuilder;

namespace SearchEngine
{
    public class DocScoreTitle
    {
        public double Score { get; set; }
        public string Title { get; set; }

        public DocScoreTitle(string title, double score)
        {
            Score = score;
            Title = title;
        }

    }
    public class Searcher
    {
        private LoadedIndex _index;
        public Searcher(LoadedIndex loadedIndex)
        {
            _index = loadedIndex;
        }
        public double TitleBoost(string title, string query)
        {
            title = title.ToLower();
            query = query.ToLower();

            if (title == query)
                return 20.0;                    // Exact title match → HUGE boost

            if (title.StartsWith(query))
                return 6.0;                     // Prefix match

            if (title.Contains(query))
                return 3.0;                     // Substring match

            // Word-level match
            var titleWords = title.Split(' ');
            if (titleWords.Contains(query))
                return 2.0;

            return 1.0;
        }

        public double ComputePhraseScoreInOrder(List<string> orderedTerms, Dictionary<string, List<int>> termPositions)
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
                        else if (diff > 1 && diff <= 3)
                            score += 4;    // near-phrase
                        else if (diff > 3 && diff <= 6)
                            score += 1;    // weak proximity
                    }
                }
            }

            return score;
        }
        public List<(string docId, double score, string title)> Search(string query, int topK = 50)
        {
            var termPositionsByDoc = new Dictionary<string, Dictionary<string, List<int>>>();

            var qterms = query.ToLowerInvariant()
                              .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(t => TextProcessor.Tokenize(t).FirstOrDefault())
                              .Where(t => !string.IsNullOrEmpty(t))
                              .ToList();
            if (qterms.Count == 0) return new List<(string, double, string)>();

            var scores = new Dictionary<string, DocScoreTitle>(StringComparer.Ordinal);
            var totalDocs = Math.Max(1, _index.docCount);
            var avgdl = Math.Max(1.0, _index.averageDocLength);

            foreach (var term in qterms)
            {
                if (!_index.Dictionary.TryGetValue(term, out var dentry)) continue;
                var postings = ParsePosting(ReadPostingList(term));
                int df = dentry.DocFreq;
                foreach (var p in postings)
                {
                    if (!termPositionsByDoc.TryGetValue(p.docId, out var docTermMap))
                        termPositionsByDoc[p.docId] = docTermMap = new Dictionary<string, List<int>>();

                    docTermMap[term] = p.positions;

                    int docLen = 100;
                    if (_index.DocInfo.TryGetValue(p.docId, out var dinfo)) docLen = Math.Max(1, dinfo.DocLength);
                    double termScore = BM25.ComputeBM25Score(p.tf, df, docLen, avgdl, totalDocs);
                    if (scores.TryGetValue(p.docId, out var old))
                    {
                        scores[p.docId].Score = old.Score + termScore;
                    }
                    else
                    {
                        double initialScore = p.offsetScore;
                        double titleBoost = TitleBoost(dinfo.Title, query);
                        scores[p.docId] = new DocScoreTitle(dinfo != null ? dinfo.Title : String.Empty, termScore * (titleBoost * initialScore));
                    }

                }
            }

            // === ⬇ PHRASE SCORING APPLIED HERE ⬇ ===
            foreach (var kv in termPositionsByDoc)
            {
                string docId = kv.Key;
                var map = kv.Value;

                // Must contain all query terms
                if (!qterms.All(t => map.ContainsKey(t)))
                    continue;

                double phraseScore = ComputePhraseScoreInOrder(qterms, map);

                scores[docId].Score += phraseScore;
            }

            // top-K via sorting (for modest K)
            var top = scores.OrderByDescending(kv => kv.Value.Score).Take(topK).Select(kv => (kv.Key, kv.Value.Score, kv.Value.Title)).ToList();
            return top;
        }
        public double CalculateSectionBoost(int sectionFlags)
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

        public List<(string docId, int tf, double offsetScore, List<int> positions)> ParsePosting(byte[] data)
        {
            var s = Encoding.UTF8.GetString(data);

            var results = new List<(string, int, double, List<int>)>();

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

            return results;
        }
        public byte[] ReadPostingList(string term)
        {
            if (!_index.Dictionary.TryGetValue(term, out var d))
                return Array.Empty<byte>();

            if (!_index.MappedIndexFiles.TryGetValue(term[0], out var mmf))
                return Array.Empty<byte>();

            // Read EXACT bytes
            using var s = mmf.CreateViewStream(d.Offset, d.length, MemoryMappedFileAccess.Read);
            byte[] buffer = new byte[d.length];
            s.Read(buffer, 0, d.length);

            return buffer;
        }
    }
}
