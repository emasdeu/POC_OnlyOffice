using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// OnlyOffice Document Converter - Handles document conversion via OnlyOffice Document Server
/// </summary>
public class OnlyOfficeConverter
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _jwtSecret;
    private readonly string _storageServerUrl;

    public OnlyOfficeConverter(string baseUrl, string jwtSecret = "", string storageServerUrl = "")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _jwtSecret = jwtSecret;
        _storageServerUrl = string.IsNullOrEmpty(storageServerUrl) ? "http://localhost:8000" : storageServerUrl.TrimEnd('/');
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Converts a document from one format to another
    /// </summary>
    /// <param name="sourceFilePath">Local path to the source file</param>
    /// <param name="outputFormat">Target format (e.g., "pdf", "docx", "xlsx")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Byte array of the converted document</returns>
    public async Task<byte[]> ConvertDocumentAsync(
        string sourceFilePath, 
        string outputFormat, 
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"Source file not found: {sourceFilePath}");
        }

        var fileBytes = await File.ReadAllBytesAsync(sourceFilePath, cancellationToken);
        return await ConvertDocumentAsync(fileBytes, Path.GetFileName(sourceFilePath), outputFormat, cancellationToken);
    }

    /// <summary>
    /// Converts document bytes from one format to another
    /// </summary>
    /// <param name="fileBytes">Bytes of the source document</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="outputFormat">Target format</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Byte array of the converted document</returns>
    public async Task<byte[]> ConvertDocumentAsync(
        byte[] fileBytes,
        string fileName,
        string outputFormat,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Prepare conversion request
        var conversionRequest = new ConversionRequest
        {
            Async = false,
            FileType = GetFileExtension(fileName),
            Key = Guid.NewGuid().ToString(),
            OutputType = outputFormat.ToLower(),
            Title = fileName,
            Url = await UploadFileAsync(fileBytes, fileName, cancellationToken)
        };

        // Step 2: Generate JWT token if enabled
        if (!string.IsNullOrEmpty(_jwtSecret))
        {
            conversionRequest.Token = GenerateJwtToken(conversionRequest);
        }

        // Step 3: Send conversion request
        var requestJson = JsonSerializer.Serialize(conversionRequest);
        Console.WriteLine($"Request JSON: {requestJson}");
        Console.WriteLine($"Conversion request details:");
        Console.WriteLine($"  - FileType: {conversionRequest.FileType}");
        Console.WriteLine($"  - OutputType: {conversionRequest.OutputType}");
        Console.WriteLine($"  - Async: {conversionRequest.Async}");
        Console.WriteLine($"  - URL: {conversionRequest.Url}");
        Console.WriteLine($"  - Token present: {!string.IsNullOrEmpty(conversionRequest.Token)}");
        
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        // Use the /converter endpoint (OnlyOffice Document Server API)
        string conversionEndpoint = $"{_baseUrl}/converter";
        Console.WriteLine($"Sending conversion request to {conversionEndpoint}");
        
        // Add Accept header to request JSON response format
        using (var request = new HttpRequestMessage(HttpMethod.Post, conversionEndpoint))
        {
            request.Content = content;
            request.Headers.Add("Accept", "application/json");
            
            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"Conversion failed with status {response.StatusCode}: {errorContent}");
            }

        // Step 4: Parse response
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"Conversion response: {responseJson}");
            var conversionResponse = JsonSerializer.Deserialize<ConversionResponse>(responseJson);

            if (conversionResponse?.Error != null && conversionResponse.Error != 0)
            {
                throw new Exception($"Conversion failed with error code: {conversionResponse.Error}");
            }

            // Step 5: Download converted file
            if (!string.IsNullOrEmpty(conversionResponse?.FileUrl))
            {
                Console.WriteLine($"Downloading converted file from: {conversionResponse.FileUrl}");
                var fileBytes_result = await _httpClient.GetByteArrayAsync(
                    conversionResponse.FileUrl,
                    cancellationToken
                );
                return fileBytes_result;
            }

            throw new Exception("Conversion failed: No output URL returned");
        }
    }

    /// <summary>
    /// Uploads a file to the storage server
    /// The storage server runs as a sidecar in the OnlyOffice pod and provides URLs accessible to OnlyOffice
    /// </summary>
    private async Task<string> UploadFileAsync(
        byte[] fileBytes,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Uploading file to storage server: {fileName}");
        
        try
        {
            // Create a simple POST with raw bytes and filename in URL
            var uploadEndpoint = $"{_storageServerUrl}/upload?filename={Uri.EscapeDataString(fileName)}";
            Console.WriteLine($"Posting to {uploadEndpoint}");
            
            var content = new ByteArrayContent(fileBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PostAsync(uploadEndpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"File upload failed with status {response.StatusCode}: {errorContent}");
            }

            // Parse upload response
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var uploadResponse = JsonSerializer.Deserialize<UploadResponse>(responseJson);

            if (string.IsNullOrEmpty(uploadResponse?.FileUrl))
            {
                throw new Exception("Upload failed: No file URL returned from storage server");
            }

            Console.WriteLine($"File uploaded successfully. URL: {uploadResponse.FileUrl}");
            return uploadResponse.FileUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading file: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Generates a JWT token for the conversion request
    /// The token payload should contain the request parameters according to OnlyOffice API
    /// </summary>
    private string GenerateJwtToken(ConversionRequest request)
    {
        // If no JWT secret provided, don't generate token
        if (string.IsNullOrEmpty(_jwtSecret))
        {
            return "";
        }

        // Create JWT payload with request parameters (as per OnlyOffice documentation)
        var payload = new
        {
            filetype = request.FileType,
            key = request.Key,
            outputtype = request.OutputType,
            title = request.Title,
            url = request.Url
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);
        var headerBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" }));
        var secretBytes = Encoding.UTF8.GetBytes(_jwtSecret);

        var header = Convert.ToBase64String(headerBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payloadB64 = Convert.ToBase64String(payloadBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var signatureInput = $"{header}.{payloadB64}";
        using var hmac = new HMACSHA256(secretBytes);
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureInput));
        var signature = Convert.ToBase64String(signatureBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var token = $"{signatureInput}.{signature}";
        Console.WriteLine($"Generated JWT token (length: {token.Length})");
        return token;
    }

    /// <summary>
    /// Gets file extension from filename
    /// </summary>
    private static string GetFileExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.');
        return string.IsNullOrEmpty(extension) ? "docx" : extension.ToLower();
    }
}

/// <summary>
/// Conversion request model
/// </summary>
public class ConversionRequest
{
    [JsonPropertyName("async")]
    public bool Async { get; set; }

    [JsonPropertyName("filetype")]
    public string FileType { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("outputtype")]
    public string OutputType { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("token")]
    public string? Token { get; set; }
}

/// <summary>
/// Conversion response model
/// </summary>
public class ConversionResponse
{
    [JsonPropertyName("endConvert")]
    public bool EndConvert { get; set; }

    [JsonPropertyName("fileUrl")]
    public string? FileUrl { get; set; }

    [JsonPropertyName("percent")]
    public int Percent { get; set; }

    [JsonPropertyName("error")]
    public int? Error { get; set; }
}

/// <summary>
/// File upload response model
/// </summary>
public class UploadResponse
{
    [JsonPropertyName("fileUrl")]
    public string? FileUrl { get; set; }
}
