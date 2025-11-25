//using Models;
//using System.Collections.Concurrent;
//using System.Globalization;
//using System.Text;
//using System.Text.RegularExpressions;

//namespace WikiIndexBuilder.Obsolete
//{
//    public static class IndexFileReader
//    {
//        public static IEnumerable<PageIndexEntry> ReadIndexFile(string indexPath)
//        {
//            foreach (var line in File.ReadLines(indexPath))
//            {
//                ReadOnlySpan<char> span = line.AsSpan();
//                int colon1 = span.IndexOf(':');
//                int colon2 = span.Slice(colon1 + 1).IndexOf(':') + colon1 + 1;

//                var byteOffset = long.Parse(span.Slice(0, colon1));
//                var docId = int.Parse(span.Slice(colon1 + 1, colon2 - colon1 - 1));
//                var title = span.Slice(colon2 + 1).ToString();

//                yield return new PageIndexEntry
//                {
//                    ByteOffset = byteOffset,
//                    DocId = docId,
//                    Title = title
//                };
//            }
//        }
//    }
//    public class PageIndexEntry
//    {
//        public long ByteOffset { get; set; }
//        public int DocId { get; set; }
//        public string Title { get; set; } = string.Empty;
//    }

//    public class WikiIndexer
//    {
//        private readonly XMLHandler _xmlHandler;
//        private readonly int _batchSize;
//        private readonly int _numThreads;

//        public WikiIndexer(XMLHandler xmlHandler, int batchSize = 10000, int numThreads = 0)
//        {
//            _xmlHandler = xmlHandler;
//            _batchSize = batchSize;
//            _numThreads = numThreads > 0 ? numThreads : Environment.ProcessorCount;
//        }

//        private static readonly HashSet<string> Stopwords = new HashSet<string>
//        {
//            "i", "me", "my", "myself", "we", "our", "ours", "ourselves", "you", "your", "yours", "yourself", "yourselves", "he", "him",
//            "his", "himself", "she", "her", "hers", "herself", "it", "its", "itself", "they", "them", "their", "theirs", "themselves",
//            "what", "which", "who", "whom", "this", "that", "these", "those", "am", "is", "are", "was", "were", "be", "been", "being",
//            "have", "has", "had", "having", "do", "does", "did", "doing", "a", "an", "the", "and", "but", "if", "or", "because", "as",
//            "until", "while", "of", "at", "by", "for", "with", "about", "against", "between", "into", "through", "during", "before",
//            "after", "above", "below", "to", "from", "up", "down", "in", "out", "on", "off", "over", "under", "again", "further", "then",
//            "once", "here", "there", "when", "where", "why", "how", "all", "any", "both", "each", "few", "more", "most", "other", "some",
//            "such", "no", "nor", "not", "only", "own", "same", "so", "than", "too", "very", "s", "t", "can", "will", "just", "don", "should", "now"
//        };

//        private static readonly Regex TokenSplitRegex = new Regex(@"\W+", RegexOptions.Compiled);
//        private static readonly Regex NonEnglishRegex = new Regex(@"[^a-zA-Z0-9]", RegexOptions.Compiled);

//        private static IEnumerable<string> Tokenize(string text)
//        {
//            if (string.IsNullOrWhiteSpace(text))
//                yield break;

//            // 1️⃣ Normalize to decompose accented characters (é -> e)
//            string normalized = text.Normalize(NormalizationForm.FormD);

//            // 2️⃣ Remove diacritics (accents, tone marks, etc.)
//            var sb = new StringBuilder();
//            foreach (var c in normalized)
//            {
//                var category = CharUnicodeInfo.GetUnicodeCategory(c);
//                if (category != UnicodeCategory.NonSpacingMark)
//                    sb.Append(c);
//            }

//            string cleanText = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

//            // 3️⃣ Split into tokens by non-word characters
//            foreach (var token in TokenSplitRegex.Split(cleanText))
//            {
//                if (string.IsNullOrWhiteSpace(token))
//                    continue;

//                // 4️⃣ Remove non-English/special characters inside tokens
//                string cleanedToken = NonEnglishRegex.Replace(token, "");

//                // 5️⃣ Filter stopwords and very short tokens
//                if (!string.IsNullOrWhiteSpace(cleanedToken) &&
//                    cleanedToken.Length > 1 &&
//                    !Stopwords.Contains(cleanedToken))
//                {
//                    yield return cleanedToken;
//                }
//            }
//        }

//        /// <summary>
//        /// Build positional inverted index using per-thread shards to avoid locks
//        /// </summary>
//        /// <summary>
//        /// Stage 1: Build temporary sorted shards from batches
//        /// </summary>
//        public List<string> BuildInvertedIndex(string indexPath, string xmlPath, string shardFolder)
//        {
//            Directory.CreateDirectory(shardFolder);

//            var shardFiles = new ConcurrentBag<string>();
//            var batch = new List<PageIndexEntry>(_batchSize);

//            foreach (var entry in IndexFileReader.ReadIndexFile(indexPath))
//            {
//                batch.Add(entry);

//                if (batch.Count >= _batchSize)
//                {
//                    ProcessBatchToAlphabetShards(batch, xmlPath, shardFolder, shardFiles);
//                    batch.Clear();
//                }
//            }

//            if (batch.Count > 0)
//                ProcessBatchToAlphabetShards(batch, xmlPath, shardFolder, shardFiles);

//            return shardFiles.ToList();
//        }


//        private void ProcessBatchToAlphabetShards(List<PageIndexEntry> batch, string xmlPath, string shardFolder, ConcurrentBag<string> shardFiles)
//        {
//            var localIndices = new ConcurrentDictionary<char, ConcurrentDictionary<string, ConcurrentBag<(int docId, int pos)>>>();

//            var xmlHandler = new XMLHandler(xmlPath);
//            var offsets = batch.Select(e => e.ByteOffset).Distinct().ToList();

//            Parallel.ForEach(offsets, new ParallelOptions { MaxDegreeOfParallelism = _numThreads }, offset =>
//            {
//                var docsToProcess = batch.Where(e => e.ByteOffset == offset).Select(e => e.DocId).ToHashSet();

//                foreach (var doc in xmlHandler.ReadPages(offset, docsToProcess))
//                {
//                    var words = Tokenize(doc.Title + " " + doc.Text).ToList();

//                    for (int pos = 0; pos < words.Count; pos++)
//                    {
//                        var token = words[pos];
//                        char firstChar = char.ToUpper(token[0]);

//                        var termDict = localIndices.GetOrAdd(firstChar, _ => new ConcurrentDictionary<string, ConcurrentBag<(int, int)>>());
//                        var postings = termDict.GetOrAdd(token, _ => new ConcurrentBag<(int, int)>());
//                        postings.Add((doc.Id, pos));
//                    }
//                }
//            });

//            // Write alphabet-specific shard files
//            foreach (var kvp in localIndices)
//            {
//                char alpha = kvp.Key;
//                var termDict = kvp.Value;

//                var lines = termDict.OrderBy(t => t.Key)
//                                    .SelectMany(t =>
//                                        t.Value.GroupBy(p => p.docId)
//                                               .Select(g => $"{t.Key}:{g.Key}:{string.Join(",", g.Select(x => x.pos))}")
//                                    ).ToList();

//                string shardPath = Path.Combine(shardFolder, $"{alpha}_shard_{Guid.NewGuid()}.txt");
//                File.WriteAllLines(shardPath, lines);
//                shardFiles.Add(shardPath);
//            }
//        }

//        /// <summary>
//        /// Merge shards for a single alphabet into a final alphabet index
//        /// </summary>
//        public void MergeShards(List<string> shardFiles, string outputPath)
//        {
//            var readers = shardFiles.Select(f => File.OpenText(f)).ToList();
//            var currentLines = readers.Select(r => r.ReadLine()).ToList();

//            using var writer = new StreamWriter(outputPath);

//            while (currentLines.Any(l => l != null))
//            {
//                string minTerm = currentLines
//                                 .Where(l => l != null)
//                                 .Select(l => l!.Split(':', 2)[0])
//                                 .Min()!;

//                var mergedPostings = new Dictionary<int, List<int>>();

//                for (int i = 0; i < currentLines.Count; i++)
//                {
//                    if (currentLines[i] != null && currentLines[i]!.StartsWith(minTerm + ":"))
//                    {
//                        string line = currentLines[i]!;
//                        string postingsPart = line.Substring(line.IndexOf(':') + 1);

//                        foreach (var docPosting in postingsPart.Split(';', StringSplitOptions.RemoveEmptyEntries))
//                        {
//                            var parts = docPosting.Split(':', 2);
//                            if (parts.Length != 2) continue;

//                            int docId = int.Parse(parts[0]);
//                            var positions = parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries)
//                                                     .Select(int.Parse)
//                                                     .ToList();

//                            if (!mergedPostings.ContainsKey(docId))
//                                mergedPostings[docId] = new List<int>();

//                            mergedPostings[docId].AddRange(positions);
//                        }

//                        currentLines[i] = readers[i].ReadLine();
//                    }
//                }

//                var mergedLine = string.Join(";", mergedPostings.OrderBy(k => k.Key)
//                                                                .Select(kvp => $"{kvp.Key}:{string.Join(",", kvp.Value)}"));
//                writer.WriteLine($"{minTerm}:{mergedLine}");
//            }

//            foreach (var r in readers) r.Dispose();
//        }
//    }
//}
