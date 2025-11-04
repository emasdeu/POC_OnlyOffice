using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Sockets;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // Load configuration from appsettings.json
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            // Validate command line arguments
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            var inputFilePath = args[0];
            var onlyOfficeUrl = args.Length > 1 ? args[1] : config["OnlyOffice:Url"] ?? "http://localhost:8080";
            var jwtSecret = args.Length > 2 ? args[2] : config["OnlyOffice:JwtSecret"] ?? "";
            
            // For storage server, use Kubernetes DNS name for better compatibility with port-forwards
            var defaultStorageUrl = config["OnlyOffice:StorageServerUrl"] ?? "http://onlyoffice-onlyoffice-documentserver-fileserver.onlyoffice.svc.cluster.local:9000";
            var storageServerUrl = args.Length > 3 ? args[3] : defaultStorageUrl;

            // Validate input file exists
            if (!File.Exists(inputFilePath))
            {
                Console.Error.WriteLine($"Error: Input file not found: {inputFilePath}");
                return 1;
            }

            var fileExtension = Path.GetExtension(inputFilePath).ToLower();
            Console.WriteLine($"Input file: {inputFilePath}");
            Console.WriteLine($"OnlyOffice URL: {onlyOfficeUrl}");
            Console.WriteLine($"File type: {fileExtension}");

            // Determine output format based on input
            var outputFormat = DetermineOutputFormat(fileExtension);
            Console.WriteLine($"Output format: {outputFormat}");

            // Create converter instance
            var converter = new OnlyOfficeConverter(onlyOfficeUrl, jwtSecret, storageServerUrl);

            // Perform conversion
            Console.WriteLine("\nStarting conversion...");
            var stopwatch = Stopwatch.StartNew();

            var convertedBytes = await converter.ConvertDocumentAsync(
                inputFilePath,
                outputFormat
            );

            stopwatch.Stop();

            // Generate output filename
            var outputFileName = Path.GetFileNameWithoutExtension(inputFilePath) + "." + outputFormat.ToLower();
            var outputFilePath = Path.Combine(
                Path.GetDirectoryName(inputFilePath) ?? ".",
                outputFileName
            );

            // Save converted file
            Console.WriteLine($"Saving converted file to: {outputFilePath}");
            await File.WriteAllBytesAsync(outputFilePath, convertedBytes);

            Console.WriteLine($"\n✓ Conversion completed successfully!");
            Console.WriteLine($"  - Converted file size: {convertedBytes.Length} bytes");
            Console.WriteLine($"  - Time elapsed: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  - Output file: {outputFilePath}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n✗ Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine($"  Inner error: {ex.InnerException.Message}");
            }
            return 1;
        }
    }

    static string GetLocalIpAddress()
    {
        try
        {
            var hostName = Dns.GetHostName();
            var hostEntry = Dns.GetHostEntry(hostName);
            foreach (var address in hostEntry.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    // Prefer non-loopback addresses
                    if (!address.ToString().StartsWith("127.") && !address.ToString().StartsWith("169.254"))
                    {
                        return address.ToString();
                    }
                }
            }
            return "localhost";
        }
        catch
        {
            return "localhost";
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine(@"
OnlyOffice Document Converter - Console Application

Usage:
  OnlyOfficeConsoleApp.exe <input_file> [onlyoffice_url] [jwt_secret] [storage_server_url]

Parameters:
  input_file              - Path to the document file to convert (required)
  onlyoffice_url          - Base URL of OnlyOffice Document Server (default: http://localhost:8080)
  jwt_secret              - JWT secret for authentication (optional)
  storage_server_url      - URL of storage server sidecar (default: http://localhost:8000)

Examples:
  # Convert DOCX to PDF using defaults
  OnlyOfficeConsoleApp.exe C:\documents\document.docx

  # Convert using custom server URLs
  OnlyOfficeConsoleApp.exe C:\documents\document.docx http://onlyoffice-server.com http://storage-server.com

  # Convert with JWT authentication
  OnlyOfficeConsoleApp.exe C:\documents\document.docx http://onlyoffice-server.com my-secret-key http://storage-server.com

Configuration:
  Default values are read from appsettings.json:
  - OnlyOffice:Url
  - OnlyOffice:JwtSecret
  - OnlyOffice:StorageServerUrl

Supported Input Formats:
  - Microsoft Office: .docx, .xlsx, .pptx
  - OpenDocument: .odt, .ods, .odp
  - PDF: .pdf
  - Text: .txt, .rtf
  - Other: .doc, .xls, .ppt, .csv

Output Format:
  - Input format determines output (DOCX→PDF, XLSX→PDF, PPTX→PDF, etc.)
");
    }

    static string DetermineOutputFormat(string inputExtension)
    {
        return inputExtension switch
        {
            ".docx" or ".doc" or ".odt" or ".rtf" or ".txt" => "pdf",
            ".xlsx" or ".xls" or ".ods" or ".csv" => "pdf",
            ".pptx" or ".ppt" or ".odp" => "pdf",
            ".pdf" => "docx",
            _ => "pdf"
        };
    }
}
