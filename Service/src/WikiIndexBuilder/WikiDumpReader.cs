using Models;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace WikiIndexBuilder
{
    public class XMLHandler
    {
        private readonly string _xmlFilePath;

        public XMLHandler(string xmlFilePath)
        {
            _xmlFilePath = xmlFilePath;
        }

        /// <summary>
        /// Reads pages sequentially starting from a byte offset until all docIDs in docIdsToProcess are found
        /// </summary>
        public IEnumerable<WikiDocument> ReadPages(long offset, HashSet<int> docIdsToProcess)
        {
            using var fs = File.OpenRead(_xmlFilePath);
            //fs.Seek(offset, SeekOrigin.Begin);

            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = true,
                CloseInput = false
            };

            using var reader = XmlReader.Create(fs, settings);

            WikiDocument? currentDoc = null;
            string? currentElement = null;

            while (true)
            {
                bool success;
                try
                {
                    success = reader.Read();
                }
                catch (XmlException ex)
                {
                    Console.WriteLine($"[WARN] XML error at line {ex.LineNumber}: {ex.Message}");
                    // Try to recover: move to next node if possible
                    if (!TrySkipInvalidXml(reader))
                        break;
                    continue;
                }
                if (!success || docIdsToProcess.Count == 0)
                    break;

                bool shouldYield = false;
                WikiDocument? docToYield = null;

                try
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            currentElement = reader.LocalName;
                            if (currentElement == "page")
                                currentDoc = new WikiDocument();
                            break;

                        case XmlNodeType.Text:
                            if (currentDoc != null && currentElement != null)
                            {
                                if (currentElement == "title") currentDoc.Title = reader.Value;
                                else if (currentElement == "text") currentDoc.Text = reader.Value;
                                else if (currentElement == "id" && currentDoc.Id == 0)
                                    currentDoc.Id = int.Parse(reader.Value);
                            }
                            break;

                        case XmlNodeType.EndElement:
                            if (reader.LocalName == "page" && currentDoc != null)
                            {
                                if (docIdsToProcess.Contains(currentDoc.Id))
                                {
                                    shouldYield = true;
                                    docToYield = currentDoc;
                                    docIdsToProcess.Remove(currentDoc.Id);
                                }
                                currentDoc = null;
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Skipping malformed element '{currentElement}': {ex.Message}");
                    // Continue reading next element safely
                }

                if (shouldYield && docToYield != null)
                {
                    yield return docToYield;
                }
            }
        }

        /// <summary>
        /// Attempts to skip forward when XML becomes malformed.
        /// </summary>
        private static bool TrySkipInvalidXml(XmlReader reader)
        {
            try
            {
                // Try to move forward a few times to escape bad XML fragments
                for (int i = 0; i < 5; i++)
                {
                    if (reader.Read()) return true;
                }
            }
            catch
            {
                // Even recovery failed
            }
            return false;
        }
    }
}
