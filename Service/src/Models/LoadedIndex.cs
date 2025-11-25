using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public record OffsetLengthEntry(long Offset, int length);
    public record TermDictionaryEntry(List<OffsetLengthEntry> OffsetList ,int DocFreq);

    public record DocumentInfo(long PageOffset, long InfoboxOffset, int DocLength, string Title);

    public class LoadedIndex
    {
        public Dictionary<string, TermDictionaryEntry> Dictionary = new(StringComparer.Ordinal);
        public Dictionary<string, DocumentInfo> DocInfo = new(StringComparer.Ordinal);

        public Dictionary<char, MemoryMappedFile> MappedIndexFiles = new();
        public int averageDocLength = 0;
        public int docCount = 0;
    }
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
}
