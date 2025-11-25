using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using Models;
using FileOperations;

namespace WikiIndexBuilder
{
    public class WikiXMLHandler
    {
        private readonly FileIOManager fileIO;
        private readonly int batchSize;

        // term → { docId → frequency }
        private readonly Dictionary<string, Dictionary<string, string>> invertedIndex = new(StringComparer.Ordinal);

        // docId → byte offset (used to locate articles quickly)
        private readonly Dictionary<string, Tuple<long,Tuple<long,Tuple<int,string>>>> docOffsets = new();

        private int pageCount = 0;
        private long _infoBoxSeekLocation = -1;
        private int _docLength = 0;
        private long _totalDocLength = 0;
        public WikiXMLHandler(FileIOManager fileIO, int batchSize = 1000)
        {
            this.fileIO = fileIO;
            this.batchSize = batchSize;
        }

        public void Parse(string xmlFilePath)
        {
            Stopwatch sw = Stopwatch.StartNew();
            Console.WriteLine("Starting XML parsing...");

            using var fs = new FileStream(xmlFilePath, FileMode.Open, FileAccess.Read);
            using var reader = XmlReader.Create(fs, new XmlReaderSettings { IgnoreWhitespace = true });

            WikiPageConcise? page = null;
            var sb = new StringBuilder();
            string currentTag = "";

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    currentTag = reader.Name.ToLower();
                    if (currentTag == "page")
                    {
                        page = new WikiPageConcise();
                        page.ByteOffset = fs.Position; // record offset for docOffsets
                    }
                    sb.Clear();
                    //if(currentTag == "redirect")
                    //{

                    //}
                }
                else if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA)
                {
                    sb.Append(reader.Value);
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    switch (reader.Name.ToLower())
                    {
                        case "title":
                            if (page != null)
                                page.Title = sb.ToString();
                            break;

                        case "id":
                            if (page != null && string.IsNullOrEmpty(page.Id))
                                page.Id = sb.ToString();
                            break;

                        case "text":
                            if (page != null)
                                page.Text = sb.ToString();
                            break;

                        case "page":
                            if (page != null)
                            {
                                ProcessPage(page);
                                if (!string.IsNullOrEmpty(page.Id))
                                    docOffsets[page.Id] = new Tuple<long, Tuple<long, Tuple<int, string>>>(page.ByteOffset,new Tuple<long,Tuple<int,string>>(_infoBoxSeekLocation,new Tuple<int,string>(_docLength,page.Title)));
                            }
                            //Console.WriteLine($"Processed page count: {pageCount} in {sw.ElapsedMilliseconds*1000} s");
                            break;
                    }
                    sb.Clear();
                }
            }

            // Final batch flush
            if (invertedIndex.Count > 0)
                fileIO.WriteTemporaryFile(invertedIndex);
            double averageDocLength = pageCount > 0 ? (double)_totalDocLength / pageCount : 0;
            Console.WriteLine($"Average document length: {averageDocLength:F2} terms.");
            docOffsets["AVERAGE_DOC_LENGTH"] = new Tuple<long, Tuple<long, Tuple<int, string>>>(0, new Tuple<long, Tuple<int, string>>(-1, new Tuple<int, string>((int)averageDocLength, String.Empty)));
            docOffsets["TOTAL_DOCS"] = new Tuple<long, Tuple<long, Tuple<int, string>>>(0, new Tuple<long, Tuple<int, string>>(-1, new Tuple<int, string>(pageCount, String.Empty)));
            // Write docOffsets to disk
            fileIO.WriteDocMetadata(docOffsets);

            Console.WriteLine($"Processed {pageCount} pages in {sw.Elapsed.TotalSeconds:F2} seconds.");
            sw.Restart();

            Console.WriteLine("Merging temporary files...");
            fileIO.MergeTemporaryFiles();
            sw.Stop();
            Console.WriteLine($"Merging completed in {sw.Elapsed.TotalSeconds:F2} seconds.");
        }

        private void ProcessPage(WikiPageConcise page)
        {
            // ✅ New structured text parsing using your TextProcessor
            //Stopwatch sw  = Stopwatch.StartNew();
            var textProcessor = new TextProcessor(page.Id, page.Title, page.Text, fileIO);
            var wordSet = textProcessor.ParseText();
            
            _infoBoxSeekLocation = textProcessor.GetInfoboxSeekLocation();
            _docLength = textProcessor.GetDocumentLength();
            _totalDocLength += _docLength;
            // sections may contain:
            // - sections["title"]
            // - sections["body"]
            // - sections["infobox"]
            // - sections["category"]
            // - sections["link"]

            // Aggregate term frequencies from all sections

            Dictionary<string, TermObject> termObjectFreq = textProcessor.GetPageTermInfo();

            // Merge localFreq into the global invertedIndex
            foreach (var word in wordSet)
            {
                TermObject freq = termObjectFreq[word];

                if (!invertedIndex.TryGetValue(word, out var docFreqs))
                {
                    docFreqs = new Dictionary<string, string>(StringComparer.Ordinal);
                    invertedIndex[word] = docFreqs;
                }

                docFreqs[page.Id] = freq.ToString();
            }

            pageCount++;

            // Write and clear every batch
            if (pageCount % batchSize == 0)
            {
                fileIO.WriteTemporaryFile(invertedIndex);
                invertedIndex.Clear();
            }
            //sw.Stop();
            //if(sw.Elapsed.TotalSeconds > 1)
            //Console.WriteLine($"Processed page {pageCount} (ID: {page.Id}) in {sw.Elapsed.TotalSeconds} s");
        }
    }
}
