using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Models;
using FileOperations;
using Microsoft.Extensions.Configuration;

namespace WikiIndexBuilder
{
    public class WikiParse : IWikiParse
    {
        private readonly string xmlFilePath;
        private readonly string indexFolderPath;
        private IConfiguration _configuration;

        public WikiParse(IConfiguration _config)
        {
            _configuration = _config;
            this.xmlFilePath = _configuration["AppSettings:XmlPath"];
            this.indexFolderPath = _configuration["AppSettings:IndexFolder"];
        }

        public void Parse()
        {
            var fileIO = new FileOperations.FileIOManager(indexFolderPath);
            var handler = new WikiXMLHandler(fileIO);
            handler.Parse(xmlFilePath);
            Console.WriteLine("Index build complete.");
        }
    }
}
