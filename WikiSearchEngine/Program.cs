// See https://aka.ms/new-console-template for more information
using SearchEngine;
using System.Diagnostics;
using WikiIndexBuilder;
using WikiIndexBuilder;

//Console.WriteLine("Hello, World!");

//string xmlPath = "C:\\Users\\91928\\Downloads\\enwiki-20251020-pages-articles-multistream1.xml-p1p41242.bz2";
string xmlPath = "C:\\Users\\91928\\Downloads\\enwiki-20251020-pages-articles-multistream1.xml-p1p41242\\enwiki-20251020-pages-articles-multistream1.xml-p1p41242";
//string xmlPath = "C:\\Users\\91928\\Downloads\\enwiki-20251001-pages-articles-multistream.xml.bz2";

string indexPath = "C:\\Users\\91928\\Downloads\\enwiki-20251020-pages-articles-multistream-index1.txt-p1p41242\\enwiki-20251020-pages-articles-multistream-index1.txt-p1p41242";

string outputPath = "C:\\Users\\91928\\Downloads\\InvertedIndex";
string finalIndex = "C:\\Users\\91928\\Downloads\\InvertedIndex\\global_index.txt";

//var parser = new WikiParse(
//    xmlFilePath: xmlPath,
//    indexFolderPath: outputPath
//);

//parser.Parse();

SearchIndexLoader searchIndexLoader = new SearchIndexLoader(outputPath);
LoadedIndex index = searchIndexLoader.Load();

Searcher searcher = new Searcher(index);

Console.WriteLine("\nEnter search queries below.");
Console.WriteLine("Type 'exit' to quit.");
Console.WriteLine("------------------------------");

while (true)
{
    Console.Write("\nSearch> ");
    string? query = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(query))
        continue;

    if (query.Trim().ToLower() == "exit")
        break;

    var sw = System.Diagnostics.Stopwatch.StartNew();

    var results = searcher.Search(query, topK: 50);

    sw.Stop();
    Console.WriteLine($"\nResults ({results.Count}) [Time: {sw.Elapsed.TotalMilliseconds} ms]");

    foreach (var (docId, score, title) in results)
    {
        //string title = searcher.GetTitle(docId, indexFolder);
        Console.WriteLine($"{docId}  |  Score: {score}  |  Title: {title}");
    }
}