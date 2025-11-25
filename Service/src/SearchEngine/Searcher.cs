using Models;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Utils;

namespace SearchEngine
{
    
    public class Searcher : ISearcher
    {
        private LoadedIndex _index;
        public Searcher(ISearchIndexLoader indexLoader)
        {
            _index = indexLoader.Load();
        }

        public List<(string docId, double score, string title)> Search(string query, int topK = 50)
        {
            var termPositionsByDoc = new Dictionary<string, Dictionary<string, List<int>>>();

            var qterms = query.ToLowerInvariant()
                              .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(t => Tokenizer.Tokenize(t).FirstOrDefault())
                              .Where(t => !string.IsNullOrEmpty(t))
                              .ToList();
            if (qterms.Count == 0) return new List<(string, double, string)>();

            var scores = new Dictionary<string, DocScoreTitle>(StringComparer.Ordinal);
            var totalDocs = Math.Max(1, _index.docCount);
            var avgdl = Math.Max(1.0, _index.averageDocLength);

            foreach (var term in qterms)
            {
                if (!_index.Dictionary.TryGetValue(term, out var dentry)) continue;
                var postings = SearchUtils.ParsePosting(ReadPostingList(term));
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
                        double titleBoost = SearchUtils.TitleBoost(dinfo.Title, query);
                        scores[p.docId] = new DocScoreTitle(dinfo != null ? dinfo.Title : String.Empty, termScore * (titleBoost * initialScore));
                    }

                }
            }

            // === ⬇ PHRASE SCORING APPLIED HERE ⬇ ===
            if (qterms.Count > 1)
            {
                foreach (var kv in termPositionsByDoc)
                {
                    string docId = kv.Key;
                    var map = kv.Value;

                    // Must contain all query terms
                    if (!qterms.All(t => map.ContainsKey(t)))
                        continue;

                    double phraseScore = SearchUtils.ComputePhraseScoreInOrder(qterms, map);

                    scores[docId].Score += phraseScore;
                }
            }

            // top-K via sorting (for modest K)
            var top = scores.OrderByDescending(kv => kv.Value.Score).Take(topK).Select(kv => (kv.Key, kv.Value.Score, kv.Value.Title)).ToList();
            return top;
        }

        private List<byte[]> ReadPostingList(string term)
        {
            if (!_index.Dictionary.TryGetValue(term, out var d))
                return new List<byte[]>();

            if (!_index.MappedIndexFiles.TryGetValue(term[0], out var mmf))
                return new List<byte[]>();

            var buffers = new List<byte[]>();
            foreach (var kv in d.OffsetList)
            {
                using var s = mmf.CreateViewStream(kv.Offset, kv.length, MemoryMappedFileAccess.Read);
                byte[] tempBuffer = new byte[kv.length];
                s.Read(tempBuffer, 0, kv.length);
                buffers.Add(tempBuffer);
            }
            return buffers;
        }

    }
}
