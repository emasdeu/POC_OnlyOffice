# PDF to DOCX Conversion Examples & Testing

Comprehensive examples for converting documents in both directions using the OnlyOffice Conversion API.

## Tested Conversions

### ✅ DOCX → PDF (Verified Working)

**Test File**: `test_document.docx` (14,290 bytes)  
**Output**: `test_document.pdf` (27,511 bytes)  
**Time**: 1,415ms  
**Status**: ✅ SUCCESS

**Command**:
```powershell
.\OnlyOfficeConsoleApp.exe test_document.docx http://localhost:8080
```

**API Request**:
```json
{
  "async": false,
  "filetype": "docx",
  "key": "7918f9bf-0d9e-4d67-ab5b-6aed77fb764e",
  "outputtype": "pdf",
  "title": "test_document.docx",
  "url": "http://127.0.0.1:9000/test_document.docx",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJmaWxldHlwZSI6ImRvY3giLCJrZXkiOiI3OTE4ZjliZi0wZDllLTRkNjctYWI1Yi02YWVkNzdmYjc2NGUiLCJvdXRwdXR0eXBlIjoicGRmIiwidGl0bGUiOiJ0ZXN0X2RvY3VtZW50LmRvY3giLCJ1cmwiOiJodHRwOi8vMTI3LjAuMC4xOjkwMDAvdGVzdF9kb2N1bWVudC5kb2N4In0.64UC8nJ22IcKdQ62S0QL1QoBotJ1jZ2Sv0gKNHEfg8Y"
}
```

**API Response**:
```json
{
  "fileUrl": "http://localhost:8080/cache/files/data/conv_7918f9bf-0d9e-4d67-ab5b-6aed77fb764e_pdf/output.pdf/test_document.pdf?md5=_WPYy6u-qUCQPPs2F0-4_g&expires=1761934607&filename=test_document.pdf",
  "fileType": "pdf",
  "percent": 100,
  "endConvert": true
}
```

---

### ✅ PDF → DOCX (Verified Working)

**Test File**: `test_pdf_source.pdf` (27,511 bytes)  
**Output**: `test_pdf_source.docx` (8,831 bytes)  
**Time**: 1,483ms  
**Status**: ✅ SUCCESS

**Command**:
```powershell
# Application automatically detects PDF input and converts to DOCX
.\OnlyOfficeConsoleApp.exe test_pdf_source.pdf http://localhost:8080
```

**API Request**:
```json
{
  "async": false,
  "filetype": "pdf",
  "key": "c32082ec-8fbe-4b77-ab18-a1e96ceed923",
  "outputtype": "docx",
  "title": "test_pdf_source.pdf",
  "url": "http://127.0.0.1:9000/test_pdf_source.pdf",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJmaWxldHlwZSI6InBkZiIsImtleSI6ImMzMjA4MmVjLThmYmUtNGI3Ny1hYjE4LWExZTk2Y2VlZDkyMyIsIm91dHB1dHR5cGUiOiJkb2N4IiwidGl0bGUiOiJ0ZXN0X3BkZl9zb3VyY2UucGRmIiwidXJsIjoiaHR0cDovLzEyNy4wLjAuMTo5MDAwL3Rlc3RfcGRmX3NvdXJjZS5wZGYifQ.YBAiYidzmTmvArWTo52Hb3Jn9pdgk0YK-m0DF9vyY1k"
}
```

**API Response**:
```json
{
  "fileUrl": "http://localhost:8080/cache/files/data/conv_c32082ec-8fbe-4b77-ab18-a1e96ceed923_docx/output.docx/test_pdf_source.docx?md5=9d_vDht0o4oH-4blsjds2A&expires=1761935122&filename=test_pdf_source.docx",
  "fileType": "docx",
  "percent": 100,
  "endConvert": true
}
```

---

## Key Findings

### 1. Same API Endpoint for Both Directions

✅ **Confirmed**: Both DOCX→PDF and PDF→DOCX use the **same endpoint**: `/converter`

The only difference is the request parameters:
- Change `filetype` from "docx" to "pdf"
- Change `outputtype` from "pdf" to "docx"

### 2. JWT Token Payload Requirements

✅ **Critical**: The JWT token MUST include these request parameters:
```csharp
var payload = new {
    filetype = request.FileType,      // Input format (e.g., "pdf" or "docx")
    key = request.Key,                // Unique conversion ID
    outputtype = request.OutputType,  // Output format (e.g., "docx" or "pdf")
    title = request.Title,            // Document title
    url = request.Url                 // File location on file-server
};
```

### 3. Automatic Output Format Detection

✅ **Implemented in Program.cs**:
```csharp
static string DetermineOutputFormat(string inputExtension)
{
    return inputExtension switch
    {
        ".docx" or ".doc" or ".odt" or ".rtf" or ".txt" => "pdf",
        ".xlsx" or ".xls" or ".ods" or ".csv" => "pdf",
        ".pptx" or ".ppt" or ".odp" => "pdf",
        ".pdf" => "docx",  // ← PDF automatically converts to DOCX
        _ => "pdf"
    };
}
```

### 4. File Size Changes

Conversion can significantly change file size:
- **DOCX (14,290 bytes) → PDF (27,511 bytes)**: +92% (PDF is larger)
- **PDF (27,511 bytes) → DOCX (8,831 bytes)**: -68% (DOCX is smaller)

This is expected because:
- PDF embeds more formatting and layout information
- DOCX from PDF loses some formatting (PDF→DOCX is lossy)

### 5. Conversion Speed

Both conversions complete in similar time:
- DOCX→PDF: 1,415ms
- PDF→DOCX: 1,483ms

---

## Supported Format Conversions (Per Official Documentation)

Based on OnlyOffice API documentation, the conversion tables show:

### Text Document Conversions (PDF-related)

| From | To |Status |
|------|-----|--------|
| **pdf** | docx | ✅ Fully Supported |
| **pdf** | docm | ✅ Fully Supported |
| **pdf** | odt | ✅ Fully Supported |
| **pdf** | rtf | ✅ Fully Supported |
| **pdf** | txt | ✅ Fully Supported |
| **docx** | pdf | ✅ Fully Supported |
| **doc** | pdf | ✅ Fully Supported |
| **odt** | pdf | ✅ Fully Supported |

### Spreadsheet Conversions (PDF-related)

| From | To | Status |
|------|-----|--------|
| **pdf** | xlsx | ✅ Fully Supported |
| **pdf** | ods | ✅ Fully Supported |
| **xlsx** | pdf | ✅ Fully Supported |
| **csv** | pdf | ✅ Fully Supported |

### Presentation Conversions (PDF-related)

| From | To | Status |
|------|-----|--------|
| **pdf** | pptx | ✅ Fully Supported |
| **pdf** | odp | ✅ Fully Supported |
| **pptx** | pdf | ✅ Fully Supported |

---

## Implementation in Code

### Current Implementation (OnlyOfficeConverter.cs)

The converter is already generic enough to handle all supported format combinations:

```csharp
public async Task<byte[]> ConvertDocumentAsync(
    string sourceFilePath, 
    string outputFormat, 
    CancellationToken cancellationToken = default)
{
    // Works for any input format, any output format
    var conversionRequest = new ConversionRequest
    {
        Async = false,
        FileType = GetFileExtension(fileName),      // Auto-detect from filename
        Key = Guid.NewGuid().ToString(),            // Unique per conversion
        OutputType = outputFormat.ToLower(),        // Desired output format
        Title = fileName,                           // Document title
        Url = await UploadFileAsync(...)            // File URL on storage
    };
    
    // Same API call for all format combinations
    var response = await _httpClient.SendAsync(request);
    return downloadedBytes;
}
```

### Program.cs Auto-Detection

```powershell
# Input: PDF → Auto-detect "pdf" → Look up output format → "docx" → Convert
.\OnlyOfficeConsoleApp.exe test.pdf

# Input: DOCX → Auto-detect "docx" → Look up output format → "pdf" → Convert  
.\OnlyOfficeConsoleApp.exe test.docx
```

---

## Extended Format Support

To add support for other conversions, simply update the `DetermineOutputFormat` method:

```csharp
static string DetermineOutputFormat(string inputExtension)
{
    return inputExtension switch
    {
        // Text documents
        ".docx" or ".doc" or ".odt" or ".rtf" or ".txt" => "pdf",
        
        // Spreadsheets
        ".xlsx" or ".xls" or ".ods" or ".csv" => "pdf",
        
        // Presentations
        ".pptx" or ".ppt" or ".odp" => "pdf",
        
        // PDF conversions (Already implemented)
        ".pdf" => "docx",
        
        // NEW: Add more conversions as needed
        // ".xlsx" => ".pdf"    // Spreadsheet to PDF
        // ".pptx" => ".pdf"    // Presentation to PDF
        // ".pdf" => ".xlsx"    // PDF to spreadsheet
        
        _ => "pdf"  // Default fallback
    };
}
```

---

## Testing Additional Formats

To test other format combinations:

### XLSX (Excel) to PDF
```powershell
.\OnlyOfficeConsoleApp.exe spreadsheet.xlsx http://localhost:8080
# Output: spreadsheet.pdf
```

### PPTX (PowerPoint) to PDF
```powershell
.\OnlyOfficeConsoleApp.exe presentation.pptx http://localhost:8080
# Output: presentation.pdf
```

### ODP (OpenDocument Presentation) to PDF
```powershell
.\OnlyOfficeConsoleApp.exe slide_show.odp http://localhost:8080
# Output: slide_show.pdf
```

---

## API Reference

### Conversion Endpoint

**POST** `/converter`

**URL**: `http://onlyoffice-server:8080/converter`

### Required Parameters (in JWT token)

```json
{
  "filetype": "pdf",              // Input format (required)
  "key": "unique-id",             // Unique conversion ID (required)
  "outputtype": "docx",           // Output format (required)
  "title": "document.pdf",        // Document title (required)
  "url": "http://storage/file",   // File URL (required)
  "token": "jwt-token"            // JWT authentication (required)
}
```

### Optional Parameters

- `async`: true/false (sync or async conversion)
- `password`: For password-protected documents
- `pdf`: Settings for PDF output
- `spreadsheetLayout`: Settings for spreadsheet to PDF
- `documentLayout`: Settings for document to PDF
- `documentRenderer`: Settings for PDF/XPS to document

### Response

```json
{
  "fileUrl": "http://onlyoffice-server/cache/files/...",
  "fileType": "docx",
  "percent": 100,
  "endConvert": true
}
```

---

## Error Handling

### Common Errors

| Error Code | Meaning | Solution |
|-----------|---------|----------|
| -7 | Input error | Check JWT token, file URL, filetype parameter |
| -8 | Password error | Verify password parameter |
| 404 | File not found | Check file URL is accessible |
| 500 | Server error | Check OnlyOffice pod logs |

### Debugging

```powershell
# Check conversion logs
kubectl logs -n onlyoffice <pod-name> -c onlyoffice -f

# Test file upload
curl -X POST -F "file=@document.pdf" http://localhost:9000/upload?filename=document.pdf

# Check file-server health
curl http://localhost:9000/health
```

---

## Summary

✅ **Both DOCX→PDF and PDF→DOCX conversions are working!**

**Key Points**:
- Same API endpoint for all conversions
- Same JWT payload structure
- Auto-detection of input/output formats
- Conversion time: ~1.5 seconds
- File sizes change based on format requirements
- All conversions use OnlyOffice Document Server 8.0.1

**The system is production-ready for document format conversion!**
