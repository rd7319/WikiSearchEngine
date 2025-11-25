using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public struct SearchResult
    {
        public String docId { get; set; } 
        public String docUrl { get; set; }
        public Double searchScore { get; set; }
        public String title { get; set; }

        public SearchResult(String _docId, String _docUrl, Double _searchScore,String _title)
        {
            docId = _docId;
            docUrl = _docUrl;
            searchScore = _searchScore;
            title = _title;
        }

        //public List<ResultLine> SearchResults { get; set; }
    }
    
}
