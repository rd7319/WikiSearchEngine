using Microsoft.AspNetCore.Mvc;
using Models;

namespace WikiSearch.API.Controllers
{
    public class IndexController : Controller
    {
        private IWikiParse _parser;
        public IndexController(IWikiParse wikiParse)
        {
            _parser = wikiParse;
        }
        [HttpGet]
        public async Task<ActionResult> BuildIndex()
        {
            try
            {
                _parser.Parse();
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ($"Internal server error: {ex.Message}"));
            }
        }
    }
}
