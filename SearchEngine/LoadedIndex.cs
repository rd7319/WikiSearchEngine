using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchEngine
{
    public record TermDictionaryEntry(long Offset, int length,int DocFreq);

    public record DocumentInfo(long PageOffset, long InfoboxOffset, int DocLength, string Title);

    public class LoadedIndex
    {
        public Dictionary<string, TermDictionaryEntry> Dictionary = new(StringComparer.Ordinal);
        public Dictionary<string, DocumentInfo> DocInfo = new(StringComparer.Ordinal);

        public Dictionary<char, MemoryMappedFile> MappedIndexFiles = new();
        public int averageDocLength = 0;
        public int docCount = 0;
    }
}
