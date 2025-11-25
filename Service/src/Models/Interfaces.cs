using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public interface ISearcher
    {
        List<(string docId, double score, string title)> Search(string query, int topK);
    }

    public interface IWikiParse
    {
        void Parse();
    }
    public interface ISearchIndexLoader
    {
        LoadedIndex Load();
    }
}
