using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiIndexBuilder
{
    public class WikiParse
    {
        private readonly string xmlFilePath;
        private readonly string indexFolderPath;

        public WikiParse(string xmlFilePath, string indexFolderPath)
        {
            this.xmlFilePath = xmlFilePath;
            this.indexFolderPath = indexFolderPath;
        }

        public void Parse()
        {
            var fileIO = new FileIOManager(indexFolderPath);
            var handler = new WikiXMLHandler(fileIO);
            handler.Parse(xmlFilePath);
            Console.WriteLine("✅ Index build complete.");
        }
    }
}
