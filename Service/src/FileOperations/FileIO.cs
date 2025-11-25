using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileOperations
{
    public class FileIOManager
    {
        private readonly string indexFolder;
        private int fileCounter = 0;
        private readonly object fileLock = new();
        private readonly string indexFolderPath;
        private readonly string temporaryFilePrefix;
        private readonly StreamWriter dictionaryWriter;
        private const int WORD_BUFFER_SIZE = 1000;

        public FileIOManager(string indexFolder)
        {
            this.indexFolder = indexFolder;
            Directory.CreateDirectory(indexFolder);
            this.indexFolderPath = indexFolder; // Ensure indexFolderPath is initialized
            this.temporaryFilePrefix = "temp_"; // Ensure temporaryFilePrefix is initialized
            this.dictionaryWriter = new StreamWriter(Path.Combine(indexFolderPath, "dictionary.idx"), false, Encoding.UTF8);
        }

        public string WriteTemporaryFile(Dictionary<string, Dictionary<string, string>> invertedIndex)
        {
            string filePath;
            lock (fileLock)
            {
                filePath = Path.Combine(indexFolder, $"temp_{fileCounter++}.idx");
            }

            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            foreach (var (term, postings) in invertedIndex.OrderBy(x => x.Key))
            {
                string postingLine = $"{term}:{string.Join(",", postings.Select(p => $"{p.Key}:{p.Value}"))}";
                writer.WriteLine(postingLine);
            }

            return filePath;
        }

        public void MergeTemporaryFiles()
        {
            int countTemporaryFile = Directory.GetFiles(indexFolder, $"{temporaryFilePrefix}*.idx").Length;
            if (countTemporaryFile <= 1)
            {
                SplitIntoMultiple(countTemporaryFile);
                return;
            }

            // Setup 26 index partitions and secondary index filenames
            string[] fileNames = Enumerable.Range('a', 26).Select(c => $"index{(char)c}.idx").ToArray();
            string[] sfileNames = Enumerable.Range('a', 26).Select(c => $"sindex{(char)c}.idx").ToArray();

            long[] fileSeeks = new long[fileNames.Length];      // current byte offset inside indexX.idx
            int fileSeekDictionary = 0;                         // current byte offset in dictionary
            int[] wordCount = new int[sfileNames.Length];       // secondary index counters
            const int WORD_BUFFER_SIZE = 1000;

            // Writers for final index files and secondary index files
            var writers = new StreamWriter[fileNames.Length];
            var swriters = new StreamWriter[sfileNames.Length];
            for (int i = 0; i < fileNames.Length; i++)
            {
                // Use UTF8 and ensure we write '\n' explicitly later for predictable offsets.
                writers[i] = new StreamWriter(Path.Combine(indexFolderPath, fileNames[i]), false, new UTF8Encoding(false));
                swriters[i] = new StreamWriter(Path.Combine(indexFolderPath, sfileNames[i]), false, new UTF8Encoding(false));
                fileSeeks[i] = 0;
                wordCount[i] = 0;
            }

            // Readers for temporary files
            var tempPaths = Directory.GetFiles(indexFolder, $"{temporaryFilePrefix}*.idx")
                                     .OrderBy(p => p) // ensure deterministic order
                                     .ToArray();
            int n = tempPaths.Length;
            var readers = new StreamReader[n];
            for (int i = 0; i < n; i++) readers[i] = new StreamReader(tempPaths[i], Encoding.UTF8);

            // Priority queue keyed by term only, value carries the whole line + fileIndex
            // Using SortedSet for deterministic ordering; each entry: (term, fileIndex, fullLine)
            var comparer = Comparer<(string term, int fileIndex)>.Create((a, b) =>
            {
                int c = string.CompareOrdinal(a.term, b.term);
                return c != 0 ? c : a.fileIndex.CompareTo(b.fileIndex);
            });
            var pq = new SortedSet<(string term, int fileIndex)>(comparer);

            // We also keep a mapping from key to the full line (since SortedSet stores only (term,fileIndex)).
            var currentLineFor = new Dictionary<int, string>(n);

            // Initialize: read first line from each temp file and push into pq
            for (int i = 0; i < n; i++)
            {
                var line = readers[i].ReadLine();
                if (!string.IsNullOrEmpty(line))
                {
                    string term = ExtractTerm(line);
                    currentLineFor[i] = line;
                    pq.Add((term, i));
                }
                else
                {
                    readers[i].Close();
                    readers[i] = null;
                }
            }

            // Dictionary writer (term -> offset in corresponding indexX.idx)
            using var dictWriter = this.dictionaryWriter;

            while (pq.Count > 0)
            {
                // get smallest term
                var min = pq.Min;
                pq.Remove(min);
                string term = min.term;

                // Collect and merge postings for this term from any readers whose current term == term
                // mergedPosting is the final posting string to write to index file (we assume temp files store docId:freq,docId:freq,...)
                var postingsParts = new List<string>();

                // Because pq only gave us one (term,fileIndex), we need to check all readers whether their current term matches `term`.
                // We'll iterate over a snapshot of current pq entries matching this term by checking currentLineFor entries.
                var matchingFileIndices = new List<int>();

                // First add the fileIndex we popped
                matchingFileIndices.Add(min.fileIndex);

                // Also check other files: (We could have enqueued them already)
                // We'll scan all currentLineFor entries; if their extracted term equals `term` and they haven't been processed, add them.
                while (pq.Count > 0 && pq.Min.term == term)
                {
                    int fi = pq.Min.fileIndex;
                    pq.Remove(pq.Min);
                    matchingFileIndices.Add(fi);
                }

                // For each matching file, extract posting, add to list, advance reader and update pq/currentLineFor
                foreach (int fi in matchingFileIndices)
                {
                    // Get full line
                    if (!currentLineFor.TryGetValue(fi, out var fullLine)) continue;

                    // Extract posting portion (everything after first ':')
                    int pos = fullLine.IndexOf(':');
                    if (pos >= 0 && pos + 1 < fullLine.Length)
                    {
                        string posting = fullLine.Substring(pos + 1);
                        postingsParts.Add(posting.Trim());
                    }

                    // Advance this reader: read next line and update structures
                    string next = readers[fi]?.ReadLine();
                    if (!string.IsNullOrEmpty(next))
                    {
                        currentLineFor[fi] = next;
                        string nextTerm = ExtractTerm(next);
                        pq.Add((nextTerm, fi));
                    }
                    else
                    {
                        // no more lines in this reader
                        currentLineFor.Remove(fi);
                        if (readers[fi] != null)
                        {
                            readers[fi].Close();
                            readers[fi] = null;
                        }
                    }

                    // Also, if this file had an entry in pq for this term (other positions), remove them to avoid re-processing.
                    // (We already removed the popped min; others will be re-added only if their nextTerm matches.)
                }

                // Merge postingParts into a single posting string. Here we assume postingsParts contain strings like:
                // "docA:tfA,docB:tfB" etc. We must combine them by docId summation.
                Tuple<String, int> mergedTuple = MergePostings(postingsParts); // function below handles merging docId:freq lists
                String mergedPosting = mergedTuple.Item1;
                int docCount = mergedTuple.Item2;
                // Now write dictionary entry and posting into the appropriate partition file
                char startChar = char.ToLower(term[0]);
                int idx = startChar - 'a';
                if (idx < 0 || idx >= 26)
                {
                    // skip terms not starting a-z (or handle separately)
                    continue;
                }

                long startOffset = fileSeeks[idx];

                // write posting with explicit '\n' (so we count bytes exactly)
                string postingLine = mergedPosting + '\n'; // e.g. "11142:2,11227:1,..."
                writers[idx].Write(postingLine);
                //writers[idx].Write('\n'); // explicit single newline
                int length = Encoding.UTF8.GetByteCount(postingLine);// +1 for '\n' byte
                fileSeeks[idx] += length;

                // dictionary entry: "<term>:<byteOffset>\n"
                string dictionaryEntry = $"{term}:{startOffset}:{length}:{docCount}\n";
                dictWriter.Write(dictionaryEntry);

                // write secondary index periodically
                if (wordCount[idx] == 0)
                {
                    string sEntry = $"{term}:{fileSeekDictionary}\n";
                    swriters[idx].Write(sEntry);
                    wordCount[idx] = WORD_BUFFER_SIZE;
                }
                wordCount[idx]--;
                fileSeekDictionary += Encoding.UTF8.GetByteCount(dictionaryEntry);
            }

            // close writers
            for (int i = 0; i < writers.Length; i++)
            {
                writers[i]?.Flush();
                writers[i]?.Close();
                swriters[i]?.Flush();
                swriters[i]?.Close();
            }

            // close any remaining readers and delete temp files
            for (int i = 0; i < n; i++)
            {
                readers[i]?.Close();
                File.Delete(tempPaths[i]);
            }

            dictWriter.Flush();
            dictWriter.Close();
            Console.WriteLine("Merge completed successfully.");
        }

        // Helper: extract term (text before first ':')
        private string ExtractTerm(string line)
        {
            int idx = line.IndexOf(':');
            return idx >= 0 ? line.Substring(0, idx) : line;
        }

        // Helper: merge postingsParts where each part is "doc:freq,doc:freq,...".
        // Returns a single merged string "doc:freq,doc2:freq2,..." with frequencies summed.
        private Tuple<String,int> MergePostings(IEnumerable<string> postingsParts)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            int docCount = 0;
            foreach (var part in postingsParts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                var items = part.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var it in items)
                {
                    var kv = it.Split(':', 2);
                    if (kv.Length != 2) continue;
                    string docId = kv[0].Trim();
                    var freqArr = kv[1].Split("$", 2);
                    if(freqArr.Length != 2) continue;
                    var posArr = freqArr[1].Split('|');
                    if (!int.TryParse(posArr[0].Trim(), out int freq)) continue;
                    String sectionFlag = freqArr[0].Trim();
                    String mapKey = $"{docId}${sectionFlag}";
                    if (map.ContainsKey(mapKey)) map[mapKey] = freqArr[1].Trim();
                    else
                    {
                        docCount++;
                        map[mapKey] = freqArr[1].Trim();
                    }
                }
            }

            // order by docId
            var ordered = map.OrderBy(kvp => int.TryParse(kvp.Key, out var id) ? id : int.MaxValue);
            return new Tuple<String,int>(string.Join(",", ordered.Select(kvp => $"{kvp.Key}:{kvp.Value}")),docCount);
        }

        // Dummy placeholder
        private void SplitIntoMultiple(int count)
        {
            Console.WriteLine($"Only one temporary file — skipping merge ({count}).");
        }
        public void WriteDocMetadata(Dictionary<string, Tuple<long,Tuple<long,Tuple<int, string>>>> docOffsets)
        {
            string path = Path.Combine(indexFolder, "doc_offsets.txt");
            using var writer = new StreamWriter(path, false);
            foreach (var kv in docOffsets)
                writer.WriteLine($"{kv.Key}:{kv.Value.Item1},{kv.Value.Item2.Item1},{kv.Value.Item2.Item2.Item1},{kv.Value.Item2.Item2.Item2}");
        }

        /// <summary>
        /// Appends infobox text to infobox.txt and returns its byte offset.
        /// </summary>
        public long DumpInfoInformation(string infoboxContent)
        {
            if (string.IsNullOrWhiteSpace(infoboxContent))
                return -1;

            // Ensure consistent encoding
            byte[] bytes = Encoding.UTF8.GetBytes(infoboxContent + Environment.NewLine);

            long offset;
            string path = Path.Combine(indexFolder, "info_box.txt");
            // Open file in append mode and write
            using (var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
            {
                offset = fs.Length; // current byte offset before writing
                fs.Seek(0, SeekOrigin.End);
                fs.Write(bytes, 0, bytes.Length);
            }

            return offset;
        }
    }
}
