using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SearchEngine
{
    public class SearchIndexLoader
    {
        private readonly string folder;
        private LoadedIndex _index;

        public SearchIndexLoader(string indexFolder)
        {
            folder = indexFolder.TrimEnd('/', '\\') + Path.DirectorySeparatorChar;
            _index = new LoadedIndex();
        }

        public LoadedIndex Load()
        {
            

            Console.WriteLine("Loading dictionary into memory...");
            LoadDictionary();

            Console.WriteLine("Loading document metadata...");
            LoadDocumentInfo();

            Console.WriteLine("Memory-mapping posting files...");
            MapPostingFiles();

            Console.WriteLine("Index successfully loaded into memory");
            return _index;
        }
        private void LoadDictionary()
        {
            string dictFile = folder + "dictionary.idx";
            using var reader = new StreamReader(dictFile);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                int colon = line.IndexOf(':');
                if (colon == -1) continue;

                string term = line[..colon];
                string[] parts = line[(colon + 1)..].Split(':');

                long offset = long.Parse(parts[0]);
                int length = int.Parse(parts[1]);
                int df = int.Parse(parts[2]);

                _index.Dictionary[term] = new TermDictionaryEntry(offset,length, df);
            }
        }
        private void LoadDocumentInfo()
        {
            string metaFile = folder + "doc_offsets.txt";
            using var reader = new StreamReader(metaFile);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                int colon = line.IndexOf(':');
                if (colon == -1) continue;

                string docId = line[..colon];

                string[] parts = line[(colon + 1)..].Split(',');

                long pageOffset = long.Parse(parts[0]);
                long infoboxOffset = long.Parse(parts[1]);
                int docLen = int.Parse(parts[2]);
                string title = parts[3];
                if(docId == "AVERAGE_DOC_LENGTH")
                {
                    _index.averageDocLength = docLen;
                    continue;
                }
                if (docId == "TOTAL_DOCS")
                {
                    _index.docCount = docLen;
                    continue;
                }
                _index.DocInfo[docId] = new DocumentInfo(pageOffset, infoboxOffset, docLen,title);
            }
        }
        private void MapPostingFiles()
        {
            for (char c = 'a'; c <= 'z'; c++)
            {
                string path = folder + $"index{c}.idx";
                if (!File.Exists(path)) continue;

                _index.MappedIndexFiles[c] =
                    MemoryMappedFile.CreateFromFile(path, FileMode.Open, $"idx_{c}", 0L, MemoryMappedFileAccess.Read);
            }
        }
    }
}
