# WikiSearchEngine

WikiSearchEngine is a .NET 8-based application designed to parse, index, and search Wikipedia XML dumps efficiently. It provides a robust backend for building a search engine capable of handling large-scale data.

## Features

- **XML Parsing**: Extracts and processes data from Wikipedia XML dumps.
- **Indexing**: Builds an inverted index for efficient search operations.
- **Search Functionality**: Supports querying indexed data with high performance.
- **Modular Design**: Includes components for file operations, text processing, and API integration.
- **API Integration**: Provides a RESTful API for search operations using ASP.NET Core.

## Prerequisites

- .NET 8 SDK
- A Wikipedia XML dump file
- Visual Studio 2022 or any compatible IDE

## Installation

1. Clone the repository:git clone https://github.com/rd7319/WikiSearchEngine.git cd WikiSearchEngine

2. Open the solution in Visual Studio 2022.

3. Restore NuGet packages:dotnet restore

4. Build the solution:dotnet build

## Configuration

Update the `appsettings.json` file with the paths for the XML dump and the index folder:{ "AppSettings": { "XmlPath": "path/to/your/xmlfile.xml", "IndexFolder": "path/to/index/folder" } }

## Usage

### Download Dumps
Download the latest dumps from https://dumps.wikimedia.org/enwiki/

### Indexing
Run the `WikiIndexBuilder` project to parse the XML dump and build the index

### Searching
Start the `WikiSearch.API` project to launch the RESTful API: dotnet run --project WikiSearch.API

Access the Swagger UI at `http://localhost:5000/swagger` to test the API.

## Project Structure

- **WikiIndexBuilder**: Handles XML parsing and index building.
- **FileOperations**: Manages file I/O operations for indexing.
- **WikiSearch.API**: Provides a RESTful API for search functionality.
- **Models**: Contains shared data models.

## Dependencies

- [Swashbuckle.AspNetCore](https://github.com/domaindrivendev/Swashbuckle.AspNetCore) for API documentation.

## Contributing

Contributions are welcome! Please fork the repository and submit a pull request.