using System.Net;
using System.Net.Mime;
using System.Net.Sockets;

/// <summary>
/// Simple HTTP file server to serve uploaded files to OnlyOffice
/// </summary>
public class SimpleFileServer : IDisposable
{
    private HttpListener? _listener;
    private Thread? _listenerThread;
    private readonly string _basePath;
    private readonly int _port;
    private bool _isRunning;
    private readonly string _hostAddress;

    public string ServerUrl => $"http://{_hostAddress}:{_port}";

    public SimpleFileServer(string basePath, int port = 9999, string? hostAddress = null)
    {
        _basePath = basePath;
        _port = port;
        _hostAddress = hostAddress ?? "localhost";
    }

    public void Start()
    {
        if (_isRunning) return;

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://0.0.0.0:{_port}/");
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            // Add all local IPs
            var hostName = Dns.GetHostName();
            var hostEntry = Dns.GetHostEntry(hostName);
            foreach (var address in hostEntry.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    _listener.Prefixes.Add($"http://{address}:{_port}/");
                }
            }
            
            _listener.Start();
            _isRunning = true;

            _listenerThread = new Thread(Listen)
            {
                IsBackground = true,
                Name = "FileServerThread"
            };
            _listenerThread.Start();

            Console.WriteLine($"File server started on {ServerUrl}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start file server: {ex.Message}");
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
                ProcessRequest(context);
            }
            catch (HttpListenerException)
            {
                // Listener was closed
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in file server: {ex.Message}");
            }
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        try
        {
            string requestPath = context.Request.Url?.LocalPath ?? "/";
            
            // Remove leading slash
            if (requestPath.StartsWith("/"))
                requestPath = requestPath.Substring(1);

            string filePath = Path.Combine(_basePath, requestPath);

            // Security check: ensure the requested file is within basePath
            string fullBasePath = Path.GetFullPath(_basePath);
            string fullFilePath = Path.GetFullPath(filePath);

            if (!fullFilePath.StartsWith(fullBasePath))
            {
                context.Response.StatusCode = 403;
                context.Response.Close();
                return;
            }

            if (File.Exists(fullFilePath))
            {
                var fileInfo = new FileInfo(fullFilePath);
                context.Response.ContentLength64 = fileInfo.Length;
                context.Response.ContentType = GetMimeType(fullFilePath);
                context.Response.StatusCode = 200;

                using (FileStream fs = File.OpenRead(fullFilePath))
                {
                    fs.CopyTo(context.Response.OutputStream);
                }

                context.Response.Close();
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing request: {ex.Message}");
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch { }
        }
    }

    private static string GetMimeType(string filePath)
    {
        return Path.GetExtension(filePath).ToLower() switch
        {
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".doc" => "application/msword",
            ".xls" => "application/vnd.ms-excel",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".odt" => "application/vnd.oasis.opendocument.text",
            ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
            ".odp" => "application/vnd.oasis.opendocument.presentation",
            _ => "application/octet-stream"
        };
    }

    public void Stop()
    {
        _isRunning = false;
        _listener?.Stop();
        _listener?.Close();
        _listenerThread?.Join(2000);
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
