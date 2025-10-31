using System.Net;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// OnlyOffice Storage Server - Sidecar HTTP file server for document storage
/// Runs in the same pod as OnlyOffice and serves files on localhost:8000
/// </summary>
public class StorageServer
{
    private HttpListener? _listener;
    private Thread? _listenerThread;
    private readonly string _storagePath;
    private readonly int _port;
    private bool _isRunning;

    public StorageServer(string storagePath = "/var/lib/onlyoffice-storage", int port = 8000)
    {
        _storagePath = storagePath;
        _port = port;
    }

    public void Start()
    {
        if (_isRunning) return;

        try
        {
            // Create storage directory if it doesn't exist
            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
                Console.WriteLine($"Created storage directory: {_storagePath}");
            }

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://0.0.0.0:{_port}/");
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();
            _isRunning = true;

            _listenerThread = new Thread(Listen)
            {
                IsBackground = true,
                Name = "StorageServerListener"
            };
            _listenerThread.Start();

            Console.WriteLine($"Storage Server started on port {_port}");
            Console.WriteLine($"Storage directory: {_storagePath}");
            Console.WriteLine($"Ready for file uploads and downloads");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start storage server: {ex.Message}");
            throw;
        }
    }

    private void Listen()
    {
        if (_listener == null) return;

        while (_isRunning)
        {
            try
            {
                HttpListenerContext context = _listener.GetContext();
                
                // Handle request in background to not block listener
                _ = Task.Run(() => ProcessRequest(context));
            }
            catch (HttpListenerException)
            {
                // Listener was closed
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in storage server listener: {ex.Message}");
            }
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        try
        {
            string method = context.Request.HttpMethod;
            string path = context.Request.Url?.AbsolutePath ?? "/";

            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {method} {path}");

            if (method == "POST" && path == "/upload")
            {
                HandleUpload(context);
            }
            else if (method == "GET" && path.StartsWith("/files/"))
            {
                HandleDownload(context, path);
            }
            else if (method == "GET" && path == "/health")
            {
                HandleHealth(context);
            }
            else if (method == "GET" && path == "/")
            {
                HandleInfo(context);
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.ContentType = "application/json";
                var error = JsonSerializer.Serialize(new { error = "Not found" });
                var errorBytes = System.Text.Encoding.UTF8.GetBytes(error);
                context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                context.Response.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing request: {ex.Message}");
            try
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                var error = JsonSerializer.Serialize(new { error = ex.Message });
                var errorBytes = System.Text.Encoding.UTF8.GetBytes(error);
                context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                context.Response.Close();
            }
            catch { }
        }
    }

    private void HandleHealth(HttpListenerContext context)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        var response = JsonSerializer.Serialize(new { status = "healthy" });
        var responseBytes = System.Text.Encoding.UTF8.GetBytes(response);
        context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
        context.Response.Close();
    }

    private void HandleInfo(HttpListenerContext context)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json";
        var response = JsonSerializer.Serialize(new 
        { 
            name = "OnlyOffice Storage Server",
            version = "1.0",
            status = "running",
            storagePath = _storagePath
        });
        var responseBytes = System.Text.Encoding.UTF8.GetBytes(response);
        context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
        context.Response.Close();
    }

    private void HandleUpload(HttpListenerContext context)
    {
        try
        {
            // Parse multipart form data
            string contentType = context.Request.ContentType ?? "";
            if (!contentType.StartsWith("multipart/form-data"))
            {
                context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json";
                var error = JsonSerializer.Serialize(new { error = "Content-Type must be multipart/form-data" });
                var errorBytes = System.Text.Encoding.UTF8.GetBytes(error);
                context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                context.Response.Close();
                return;
            }

            // Extract boundary
            string boundary = contentType.Split("boundary=")[1];
            using var reader = new StreamReader(context.Request.InputStream);
            string body = reader.ReadToEnd();

            // Simple multipart parser - find file part
            string fileMarker = $"filename=\"";
            int fileMarkerPos = body.IndexOf(fileMarker);
            if (fileMarkerPos < 0)
            {
                context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json";
                var error = JsonSerializer.Serialize(new { error = "No file found in upload" });
                var errorBytes = System.Text.Encoding.UTF8.GetBytes(error);
                context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                context.Response.Close();
                return;
            }

            // Extract filename
            int filenameStart = fileMarkerPos + fileMarker.Length;
            int filenameEnd = body.IndexOf("\"", filenameStart);
            string filename = body.Substring(filenameStart, filenameEnd - filenameStart);

            // Sanitize filename
            filename = Path.GetFileName(filename); // Remove any path components
            if (string.IsNullOrWhiteSpace(filename))
            {
                context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json";
                var error = JsonSerializer.Serialize(new { error = "Invalid filename" });
                var errorBytes = System.Text.Encoding.UTF8.GetBytes(error);
                context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                context.Response.Close();
                return;
            }

            // Find file content start (after \r\n\r\n following the filename)
            string contentMarker = "\r\n\r\n";
            int contentStart = body.IndexOf(contentMarker, filenameEnd) + contentMarker.Length;
            
            // Find file content end (before boundary)
            string endBoundary = $"--{boundary}--";
            int contentEnd = body.IndexOf(endBoundary, contentStart);
            if (contentEnd < 0)
            {
                contentEnd = body.LastIndexOf("--" + boundary);
            }

            // Extract and decode file content
            string fileContentStr = body.Substring(contentStart, contentEnd - contentStart).TrimEnd();
            byte[] fileBytes = System.Text.Encoding.UTF8.GetBytes(fileContentStr);

            // Save file
            string filePath = Path.Combine(_storagePath, filename);
            File.WriteAllBytes(filePath, fileBytes);

            Console.WriteLine($"File uploaded: {filename} ({fileBytes.Length} bytes)");

            // Return success response
            var fileUrl = $"http://localhost:{_port}/files/{Uri.EscapeDataString(filename)}";
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            var successResponse = JsonSerializer.Serialize(new { fileUrl, filename });
            var successBytes = System.Text.Encoding.UTF8.GetBytes(successResponse);
            context.Response.OutputStream.Write(successBytes, 0, successBytes.Length);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during file upload: {ex.Message}");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            var error = JsonSerializer.Serialize(new { error = ex.Message });
            var errorBytes = System.Text.Encoding.UTF8.GetBytes(error);
            context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
            context.Response.Close();
        }
    }

    private void HandleDownload(HttpListenerContext context, string path)
    {
        try
        {
            // Extract filename from path: /files/filename.ext
            string filename = Uri.UnescapeDataString(path.Substring("/files/".Length));
            
            // Security check: ensure filename doesn't contain path traversal
            if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
            {
                context.Response.StatusCode = 403;
                context.Response.Close();
                return;
            }

            string filePath = Path.Combine(_storagePath, filename);

            // Check if file exists
            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = 404;
                context.Response.ContentType = "application/json";
                var error = JsonSerializer.Serialize(new { error = "File not found" });
                var errorBytes = System.Text.Encoding.UTF8.GetBytes(error);
                context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                context.Response.Close();
                Console.WriteLine($"File not found: {filename}");
                return;
            }

            // Read and serve file
            byte[] fileBytes = File.ReadAllBytes(filePath);
            string mimeType = GetMimeType(filePath);

            context.Response.StatusCode = 200;
            context.Response.ContentType = mimeType;
            context.Response.ContentLength64 = fileBytes.Length;
            context.Response.OutputStream.Write(fileBytes, 0, fileBytes.Length);
            context.Response.Close();

            Console.WriteLine($"File downloaded: {filename} ({fileBytes.Length} bytes)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during file download: {ex.Message}");
            context.Response.StatusCode = 500;
            context.Response.Close();
        }
    }

    private static string GetMimeType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();
        return extension switch
        {
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".xls" => "application/vnd.ms-excel",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".odt" => "application/vnd.oasis.opendocument.text",
            ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
            ".odp" => "application/vnd.oasis.opendocument.presentation",
            ".txt" => "text/plain",
            ".rtf" => "application/rtf",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }

    public void Stop()
    {
        _isRunning = false;
        _listener?.Stop();
        _listener?.Close();
        _listenerThread?.Join(2000);
        Console.WriteLine("Storage server stopped");
    }

    public void Dispose()
    {
        Stop();
        if (_listener != null)
        {
            ((IDisposable)_listener).Dispose();
        }
    }
}

/// <summary>
/// Main entry point for Storage Server
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== OnlyOffice Storage Server ===");
        
        // Get configuration from environment variables or defaults
        string storagePath = Environment.GetEnvironmentVariable("STORAGE_PATH") ?? "/var/lib/onlyoffice-storage";
        string portStr = Environment.GetEnvironmentVariable("LISTEN_PORT") ?? "8000";

        if (!int.TryParse(portStr, out int port))
        {
            port = 8000;
        }

        Console.WriteLine($"Storage path: {storagePath}");
        Console.WriteLine($"Listen port: {port}");

        // Create and start server
        var server = new StorageServer(storagePath, port);
        server.Start();

        Console.WriteLine("\nServer is running. Press Ctrl+C to stop.");
        
        // Keep server running
        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            server.Stop();
        };

        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            server.Stop();
            Environment.Exit(0);
        };

        // Keep main thread alive
        while (true)
        {
            Thread.Sleep(1000);
        }
    }
}
