using Microsoft.AspNetCore.Mvc;
using Models;

namespace WikiSearch.API.Controllers
{
    public class SearchController : Controller
    {
        private ISearcher _searcher;
        private String _wikiBaseUrl = "https://en.wikipedia.org/wiki/";
        public SearchController(ISearcher searcher)
        {
            _searcher = searcher;
        }
        [HttpGet("search")]
        public async Task<ActionResult> SearchWiki(
            [FromQuery] string? searchTerm,
            [FromQuery] int? maxResults)
        {
            try
            {
                var results = _searcher.Search(
                    searchTerm,
                    maxResults ?? 10
                );
                List<SearchResult> searchResponse = new();
                
                foreach(var result in results)
                {
                    var url = _wikiBaseUrl + result.title;
                    searchResponse.Add(new SearchResult(result.docId, url, result.score, result.title));
                }
                return Ok(searchResponse);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ($"Internal server error: {ex.Message}"));
            }
        }
    }
}
