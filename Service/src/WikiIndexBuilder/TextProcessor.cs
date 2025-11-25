using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using FileOperations;
using Utils;
using Models;
using System.Text.RegularExpressions;

namespace WikiIndexBuilder
{


    public class TextProcessor
    {
        // Section bitmasks
        private const int GEOBOX = 1;
        private const int INFOBOX = 2;
        private const int LINKS = 4;
        private const int BODY = 8;
        private const int CATEGORY = 16;
        private const int TITLE = 32;

        private readonly string _wikiText;
        private readonly string _wikiPageId;
        private readonly string _wikiPageTitle;
        private readonly FileIOManager _fileIO;

        private int _docLength = 0;

        private long _infoboxSeekLocation = -1;
        private readonly Dictionary<string, TermObject> _pageTermInfo = new();
        private readonly HashSet<string> _uniqueWords = new(StringComparer.OrdinalIgnoreCase);

        private PorterStemmer _stemmer;

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

        public TextProcessor(string pageId, string pageTitle, string pageText, FileIOManager fileIO)
        {
            _wikiPageId = pageId;
            _wikiPageTitle = pageTitle;
            _wikiText = pageText ?? "";
            _fileIO = fileIO;
            _stemmer = new PorterStemmer();
        }
        
        public Dictionary<string, TermObject> GetPageTermInfo() => _pageTermInfo;
        public long GetInfoboxSeekLocation() => _infoboxSeekLocation;

        public int GetDocumentLength() => _docLength;

        // === Main parsing entry ===
        public HashSet<string> ParseText()
        {
            var sb = new StringBuilder();
            bool externalLinksSection = false;
            //Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < _wikiText.Length; i++)
            {

                char c = _wikiText[i];

                if (char.IsAsciiLetter(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
                else if (c == '{')
                {
                    if (TryExtractTemplate(ref i, "{infobox", out var infobox))
                    {
                        //var beg = sw.Elapsed.TotalSeconds;
                        ProcessInfobox(infobox);
                        //if (sw.Elapsed.TotalSeconds  - beg> 0.1)
                        //{
                        //    Console.WriteLine($"[WARN] Infobox processing took {sw.Elapsed.TotalSeconds - beg} s for page {_wikiPageId} - {_wikiPageTitle}");
                        //}
                    }
                    else if (TryExtractTemplate(ref i, "{geobox", out var geobox))
                    {
                        //var beg = sw.Elapsed.TotalSeconds;
                        ProcessGeobox(geobox);
                        //if (sw.Elapsed.TotalSeconds - beg > 0.1)
                        //{
                        //    Console.WriteLine($"[WARN] Geobox processing took {sw.Elapsed.TotalSeconds - beg} s for page {_wikiPageId} - {_wikiPageTitle}");
                        //}
                    }
                    else if (TryExtractTemplate(ref i, "{cite", out _)) { /* skip */ }
                    else if (TryExtractTemplate(ref i, "{gr", out _)) { /* skip */ }
                    else if (TryExtractTemplate(ref i, "{coord", out _)) { /* skip */ }
                }
                else if (c == '[')
                {
                    if (TryExtractTemplate(ref i, "[[category:", out var cat))
                    {
                        //var beg = sw.Elapsed.TotalSeconds;
                        ProcessCategories(cat);
                        //if (sw.Elapsed.TotalSeconds - beg > 0.1)
                        //{
                        //    Console.WriteLine($"[WARN] Category processing took {sw.Elapsed.TotalSeconds - beg} s for page {_wikiPageId} - {_wikiPageTitle}");
                        //}
                    }
                    else if (TryExtractTemplate(ref i, "[[image:", out _)) continue; 
                    else if (TryExtractTemplate(ref i, "[[file:", out _)) continue;
                }
                else if (c == '<')
                {
                    if (TrySkipTag(ref i, "<!--", "-->")) continue;
                    if (TrySkipTag(ref i, "<ref>", "</ref>")) continue;
                    if (TrySkipTag(ref i, "<gallery", "</gallery>")) continue;
                }
                else if (c == '=' && i + 1 < _wikiText.Length && _wikiText[i + 1] == '=')
                {
                    //var beg = sw.Elapsed.TotalSeconds;
                    i += 2;
                    while (i < _wikiText.Length && char.IsWhiteSpace(_wikiText[i])) i++;
                    if (i + 14 < _wikiText.Length && _wikiText.Substring(i, 14).Equals("external links", StringComparison.OrdinalIgnoreCase))
                    {
                        externalLinksSection = true;
                        i += 14;
                    }
                    else
                        externalLinksSection = false;
                    //if (sw.Elapsed.TotalSeconds - beg > 0.1)
                    //{
                    //    Console.WriteLine($"[WARN] ext processing took {sw.Elapsed.TotalSeconds - beg} s for page {_wikiPageId} - {_wikiPageTitle}");
                    //}
                }
                else if (c == '*' && externalLinksSection)
                {
                    ProcessExternalLinks(ref i);
                }
                else
                {
                    if (sb.Length > 0)
                    {
                        _docLength++;
                        ProcessWord(sb.ToString(), BODY);
                        sb.Clear();
                    }
                }
            }

            if (sb.Length > 0)
                ProcessWord(sb.ToString(), BODY);

            ProcessTitle(_wikiPageTitle);
            return _uniqueWords;
        }


        // === Infobox Parser ===
        private void ProcessInfobox(string text)
        {

            //Console.WriteLine("Processing infobox...");
            var infoMap = new Dictionary<string, string>();
            var key = new StringBuilder();
            var val = new StringBuilder();

            bool parsingKey = false;
            bool parsingVal = false;

            for (int i = 9; i < text.Length; i++) // skip "{{infobox"
            {
                char c = text[i];

                if (c == '|')
                {
                    parsingKey = true;
                    key.Clear();
                    val.Clear();
                    continue;
                }

                if (parsingKey)
                {
                    if (c == '=')
                    {
                        parsingKey = false;
                        parsingVal = true;
                        continue;
                    }
                    key.Append(c);
                }
                else if (parsingVal)
                {
                    if ((c == '|' || c == '\n') && val.Length > 0)
                    {
                        infoMap[key.ToString().Trim()] = val.ToString().Trim();
                        parsingVal = false;
                    }
                    else
                        val.Append(c);
                }
            }

            if (key.Length > 0 && val.Length > 0)
                infoMap[key.ToString().Trim()] = val.ToString().Trim();

            var infoboxBuilder = new StringBuilder();
            foreach (var kv in infoMap)
            {
                infoboxBuilder.AppendLine($"{kv.Key}:{kv.Value}");
            }
            infoboxBuilder.AppendLine(":");
            //Console.WriteLine($"Infobox processed in {sw.ElapsedMilliseconds * 1000} s.");
            //sw.Restart();
            _infoboxSeekLocation = _fileIO.DumpInfoInformation(infoboxBuilder.ToString());
            //Console.WriteLine($"Infobox written to disk in {sw.ElapsedMilliseconds * 1000} s.");
        }

        private void ProcessGeobox(string text)
        {
            var sb = new StringBuilder();
            foreach (var c in text)
            {
                if (char.IsLetter(c))
                    sb.Append(c);
                else if (sb.Length > 0)
                {
                    ProcessWord(sb.ToString(), GEOBOX);
                    sb.Clear();
                }
            }
            if (sb.Length > 0)
                ProcessWord(sb.ToString(), GEOBOX);
        }

        private void ProcessCategories(string text)
        {
            var sb = new StringBuilder();
            foreach (var c in text)
            {
                if (char.IsLetter(c))
                    sb.Append(c);
                else if (sb.Length > 0)
                {
                    ProcessWord(sb.ToString(), CATEGORY);
                    sb.Clear();
                }
            }
            if (sb.Length > 0)
                ProcessWord(sb.ToString(), CATEGORY);
        }

        private void ProcessTitle(string title)
        {
            var sb = new StringBuilder();
            foreach (var c in title)
            {
                if (char.IsLetter(c))
                    sb.Append(c);
                else if (sb.Length > 0)
                {
                    ProcessWord(sb.ToString(), TITLE);
                    sb.Clear();
                }
            }
            if (sb.Length > 0)
                ProcessWord(sb.ToString(), TITLE);
        }

        private void ProcessExternalLinks(ref int i)
        {
            int start = i;
            var linkBuilder = new StringBuilder();
            while (i < _wikiText.Length && _wikiText[i] != '\n')
            {
                linkBuilder.Append(_wikiText[i]);
                i++;
            }

            //foreach (var word in Regex.Split(linkBuilder.ToString(), @"\W+"))
            //{
            //    if (word.Length > 1)
            //        ProcessWord(word, LINKS);
            //}
            var link = linkBuilder.ToString();
            var sb = new StringBuilder();
            for (int j = 0; j < link.Length; j++)
            {
                char ch = link[j];
                if (char.IsAsciiLetter(ch))
                    sb.Append(char.ToLowerInvariant(ch));
                else if (sb.Length > 0)
                {
                    ProcessWord(sb.ToString(), LINKS);
                    sb.Clear();
                }
            }
            if (sb.Length > 0)
                ProcessWord(sb.ToString(), LINKS);
        }

        // === Utility methods ===
        private bool TryExtractTemplate(ref int i, string prefix, out string result)
        {
            //Stopwatch sw = Stopwatch.StartNew();
            result = string.Empty;
            //if (i + prefix.Length >= _wikiText.Length || !_wikiText.AsSpan(i + 1).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            //    return false;
            if (i + prefix.Length >= _wikiText.Length || !_wikiText.AsSpan(i + 1, prefix.Length).Equals(prefix.AsSpan(), StringComparison.OrdinalIgnoreCase))
                return false;
            int depth = 0;
            int start = i;
            ReadOnlySpan<char> span = _wikiText.AsSpan();
            for (; i < span.Length; i++)
            {
                if (span[i] == '{') depth++;
                else if (span[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        result = _wikiText.Substring(start, i - start + 1);
                        return true;
                    }
                }
            }
            
            //if (sw.Elapsed.TotalSeconds > 0.1)
            //{
            //    Console.WriteLine($"[WARN] Extract Template took {sw.Elapsed.TotalSeconds} s for page {_wikiPageId} - {_wikiPageTitle}");
            //}
            return false;
        }

        private bool TrySkipTag(ref int i, string openTag, string closeTag)
        {
            ReadOnlySpan<char> span = _wikiText.AsSpan(i + 1);

            // Check if the text starting at i+1 matches the open tag (excluding '<')
            if (span.Length < openTag.Length - 1 ||
                !span.StartsWith(openTag.AsSpan(1), StringComparison.OrdinalIgnoreCase))
                return false;

            // Find the closing tag efficiently using Span.IndexOf
            int closeIndex = _wikiText.AsSpan(i + 1).IndexOf(closeTag.AsSpan(), StringComparison.OrdinalIgnoreCase);
            if (closeIndex == -1)
            {
                i = _wikiText.Length - 1; // move to end
            }
            else
            {
                i = i + 1 + closeIndex + closeTag.Length - 1;
            }

            return true;
        }

        private void ProcessWord(string word, int section)
        {
            //Stopwatch sw = Stopwatch.StartNew();
            if (string.IsNullOrWhiteSpace(word) || word.Length <= 1 || StopWords.Contains(word) || !char.IsAsciiLetter(word[0]))
                return;

            string w = _stemmer.Stem(word.ToLowerInvariant());

            if (!_pageTermInfo.TryGetValue(w, out var termObj))
            {
                termObj = new TermObject();
                _pageTermInfo[w] = termObj;
            }

            termObj.SectionFlags |= (byte)section;
            termObj.TermFrequency++;
            termObj.positionList.Add(_docLength);
            _uniqueWords.Add(w);
            //sw.Stop();
            //if (sw.Elapsed.TotalSeconds > 0.1)
            //{
            //    Console.WriteLine($"[WARN] Word processing took {sw.Elapsed.TotalSeconds} s for word '{word}' in page {_wikiPageId} - {_wikiPageTitle}");
            //}
        }
    }
}
