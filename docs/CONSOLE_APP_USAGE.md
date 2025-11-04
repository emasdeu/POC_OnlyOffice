# Console Application Usage Guide

Complete guide for building and using the OnlyOfficeConsoleApp command-line utility for document conversion.

## Application Overview

The **OnlyOfficeConsoleApp** is a .NET 8 console application that orchestrates document conversion using OnlyOffice Document Server via its REST API.

**Key Features**:
- Command-line interface for easy integration
- Support for 20+ document formats
- Automatic output format detection
- JWT token authentication
- Detailed error reporting
- Performance metrics
- Exit codes for scripting

## Prerequisites

- .NET 8 SDK installed
- OnlyOffice Document Server running (local or remote)
- JWT secret configured
- File server available for uploads

## Build Instructions

### Navigate to Project

```powershell
cd c:\WK_SourceCode\POC_OnlyOffice\OnlyOfficeConsoleApp
```

# Port-Forwards
Run these two commands in SEPARATE terminal windows and keep them running:

Terminal 1:
kubectl port-forward svc/onlyoffice-onlyoffice-documentserver-fileserver 9000:9000 -n onlyoffice

Terminal 2:
kubectl port-forward svc/onlyoffice-onlyoffice-documentserver-fileserver 9000:9000 -n onlyoffice

Terminal 3: All next commands


### Build for Development

```powershell
# Build in Debug mode
dotnet build --project .\OnlyOfficeConsoleApp

# Run directly from source (from root directory)
dotnet run --project .\OnlyOfficeConsoleApp -- .\test_files\input.docx http://localhost:8080
```

### Build for Release

```powershell
# Build optimized Release version
dotnet build -c Release

# Run from compiled binary
.\bin\Release\net8.0\OnlyOfficeConsoleApp.exe .\test_files\input.pdf http://localhost:8080
```

### Publish Standalone

```powershell
# Publish as self-contained executable (no .NET runtime required)
dotnet publish -c Release -r win-x64 --self-contained

# Run standalone binary
.\bin\Release\net8.0\win-x64\OnlyOfficeConsoleApp.exe input.docx http://localhost:8080
```

## Configuration

### appsettings.json

```json
{
  "OnlyOffice": {
    "Url": "http://localhost:8080",
    "JwtSecret": "your-jwt-secret-key-change-me",
    "StorageServerUrl": "http://localhost:9000"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

**Parameters**:
- `Url`: OnlyOffice server address (default: http://localhost:8080)
- `JwtSecret`: Secret key for token generation (must match server config)
- `StorageServerUrl`: File server address for uploads (default: http://localhost:9000)

### Environment Variables

Configuration precedence (highest to lowest):

1. **Command-line arguments** (overrides everything)
2. **appsettings.json** (application config)
3. **Defaults** (built-in values)

```powershell
# Set environment variables (optional)
$env:ONLYOFFICE_URL = "http://onlyoffice.example.com"
$env:ONLYOFFICE_JWT_SECRET = "your-secret-key"
$env:ONLYOFFICE_STORAGE_URL = "http://file-server.example.com:9000"
```

## Command-Line Usage

### Basic Syntax

```powershell
.\OnlyOfficeConsoleApp.exe <input-file> [onlyoffice-url] [jwt-secret] [storage-url]
```

### Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `input-file` | Yes | - | Path to document to convert |
| `onlyoffice-url` | No | http://localhost:8080 | OnlyOffice server URL |
| `jwt-secret` | No | From appsettings.json | JWT authentication secret |
| `storage-url` | No | From appsettings.json | File server upload URL |

### Usage Examples

#### Minimal (uses defaults)

```powershell
# Input file required, everything else from appsettings.json
.\OnlyOfficeConsoleApp.exe document.docx

# Expects:
# - document.docx in current directory
# - OnlyOffice at http://localhost:8080
# - File server at http://localhost:9000
# - JWT secret from appsettings.json
```

#### Specify OnlyOffice URL Only

```powershell
# Override server URL, keep other defaults
.\OnlyOfficeConsoleApp.exe document.docx http://onlyoffice.company.com

# OnlyOffice: http://onlyoffice.company.com
# File server: http://localhost:9000 (from appsettings.json)
# JWT secret: from appsettings.json
```

#### Full Configuration

```powershell
# Specify all parameters explicitly
.\OnlyOfficeConsoleApp.exe `
  document.docx `
  http://onlyoffice.company.com `
  "my-jwt-secret-key" `
  http://file-server.company.com:9000
```

#### Absolute Path

```powershell
# Use absolute file path for conversion
.\OnlyOfficeConsoleApp.exe `
  C:\Documents\report.docx `
  http://localhost:8080

# Output created in same directory as input:
# C:\Documents\report.pdf
```

#### Relative Path

```powershell
# Navigate to document directory first
cd C:\Documents
..\..\OnlyOfficeConsoleApp\bin\Release\net8.0\OnlyOfficeConsoleApp.exe report.docx
```

## Supported Format Conversions

### Input Formats Supported

```
Word Processing:
  .docx  → PDF, .rtf, .odt, .txt
  .doc   → PDF, .rtf, .odt, .docx, .txt
  .rtf   → PDF, .docx, .odt, .txt
  .odt   → PDF, .docx, .txt
  .txt   → PDF, .docx

Spreadsheets:
  .xlsx  → PDF, .xls, .ods, .csv
  .xls   → PDF, .xlsx, .ods, .csv
  .ods   → PDF, .xlsx, .csv
  .csv   → PDF, .xlsx, .ods

Presentations:
  .pptx  → PDF, .odp, .ppt
  .ppt   → PDF, .pptx, .odp
  .odp   → PDF, .pptx

PDF:
  .pdf   → .docx, .rtf, .odt, .txt
           .xlsx, .csv
           .pptx
```

### Automatic Output Format Detection

The application automatically determines output format based on input extension:

```
.docx, .doc, .rtf, .odt  → .pdf
.xlsx, .xls, .ods, .csv  → .pdf
.pptx, .ppt, .odp        → .pdf
.pdf                     → .docx (NEW)
```

### Conversion Examples

```powershell
# Word document to PDF (automatic)
.\OnlyOfficeConsoleApp.exe report.docx
# Output: report.pdf

# Excel spreadsheet to PDF (automatic)
.\OnlyOfficeConsoleApp.exe data.xlsx
# Output: data.pdf

# PowerPoint to PDF (automatic)
.\OnlyOfficeConsoleApp.exe presentation.pptx
# Output: presentation.pdf

# PDF to Word document (automatic) - NEW!
.\OnlyOfficeConsoleApp.exe document.pdf
# Output: document.docx

# PDF to Word with custom server
.\OnlyOfficeConsoleApp.exe document.pdf http://onlyoffice.company.com
# Output: document.docx
```

## PDF to DOCX Conversion

The console app now supports converting PDF files to DOCX format automatically!

### How It Works

When you provide a PDF file as input, the application:
1. Detects the `.pdf` extension
2. Automatically sets output format to `docx`
3. Uploads the PDF to the file-server
4. Sends conversion request to OnlyOffice with `filetype: "pdf"` and `outputtype: "docx"`
5. Downloads and saves the converted DOCX file

### Conversion Specifications

**PDF → DOCX**:
- Uses the same `/converter` endpoint
- Same JWT token structure (only parameters change)
- Conversion time: ~1.5 seconds
- Output is editable DOCX format
- Some formatting may be lost (PDF→DOCX is lossy conversion)

### Example

```powershell
# Convert PDF to DOCX
.\OnlyOfficeConsoleApp.exe scanned_document.pdf http://localhost:8080

# Result:
# ✓ Conversion completed successfully!
#   - Converted file size: 8,831 bytes
#   - Time elapsed: 1,483ms
#   - Output file: scanned_document.docx
```

## Output Files

### Output Location

Output files are created in the **same directory as the input file**:

```powershell
# Input: C:\Documents\report.docx
# Output: C:\Documents\report.pdf

# Input: ./files/spreadsheet.xlsx
# Output: ./files/spreadsheet.pdf
```

### Output Naming Convention

```
Input filename + converted extension = output filename

Examples:
  document.docx  → document.pdf
  report.xlsx    → report.pdf
  slides.pptx    → slides.pdf
  file.pdf       → file.docx
```

### File Information

```powershell
# Get file info after conversion
Get-Item report.pdf | Select-Object Name, Length, LastWriteTime

# Output example:
# Name        : report.pdf
# Length      : 27511 (bytes)
# LastWriteTime : 10/31/2025 18:43:27
```

## Return Values & Exit Codes

### Success (Exit Code 0)

```powershell
$LASTEXITCODE -eq 0
```

Example output:
```
✓ Conversion completed successfully!
  - Converted file size: 27,511 bytes
  - Time elapsed: 1,385 ms
  - Output file: C:\WK_SourceCode\POC_OnlyOffice\test_document.pdf
```

### Failure (Exit Code Non-Zero)

```powershell
$LASTEXITCODE -ne 0
```

Common error codes:
- **1**: File not found
- **2**: OnlyOffice connection failed
- **3**: Conversion request rejected
- **4**: File download failed
- **5**: Invalid parameters

## Error Handling

### Common Errors & Solutions

#### Error: File Not Found

```
✗ Input file not found: C:\path\to\file.docx
Exit code: 1
```

**Solution**:
```powershell
# Verify file exists
Test-Path C:\path\to\file.docx

# Use correct path
.\OnlyOfficeConsoleApp.exe .\file.docx
```

#### Error: Connection Failed

```
✗ Failed to connect to OnlyOffice server at http://localhost:8080
Exit code: 2
```

**Solution**:
```powershell
# Verify OnlyOffice is running
kubectl get pods -n onlyoffice

# Check port forwarding
kubectl port-forward svc/onlyoffice-onlyoffice-documentserver 8080:80 -n onlyoffice

# Test endpoint
curl http://localhost:8080
```

#### Error: Invalid Format

```
✗ Unsupported input format: .xyz
Exit code: 5
```

**Solution**:
```powershell
# Use supported format
# Supported: docx, xlsx, pptx, pdf, doc, xls, ppt, odt, ods, odp, rtf, csv, txt
```

#### Error: OnlyOffice Error (-7 Input Error)

```
✗ Conversion failed: OnlyOffice error code -7 (Input error)
Exit code: 3
```

**Solution**:
```powershell
# Verify JWT secret matches server configuration
# Check appsettings.json JWT secret

# Verify file uploaded correctly
kubectl exec -it -n onlyoffice <pod-name> -- ls -la /var/lib/onlyoffice-storage

# Check OnlyOffice logs
kubectl logs -n onlyoffice <pod-name> -c onlyoffice
```

## Scripting & Automation

### Batch Processing

```powershell
# Convert all .docx files to PDF
Get-ChildItem *.docx | ForEach-Object {
    .\OnlyOfficeConsoleApp.exe $_.FullName
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Converted: $($_.Name)"
    } else {
        Write-Host "✗ Failed: $($_.Name)"
    }
}
```

### Monitoring Exit Codes

```powershell
# Check if conversion succeeded
.\OnlyOfficeConsoleApp.exe document.docx

if ($LASTEXITCODE -eq 0) {
    Write-Host "Conversion successful"
} else {
    Write-Host "Conversion failed with exit code: $LASTEXITCODE"
    exit $LASTEXITCODE
}
```

### Integration with PowerShell Scripts

```powershell
# Create wrapper function
function Convert-Document {
    param(
        [string]$InputFile,
        [string]$OnlyOfficeUrl = "http://localhost:8080"
    )
    
    $appPath = ".\bin\Release\net8.0\OnlyOfficeConsoleApp.exe"
    
    & $appPath $InputFile $OnlyOfficeUrl
    
    if ($LASTEXITCODE -eq 0) {
        $outputFile = [System.IO.Path]::ChangeExtension($InputFile, ".pdf")
        return $outputFile
    } else {
        throw "Conversion failed: $InputFile"
    }
}

# Use the function
$pdf = Convert-Document "C:\Documents\report.docx"
Write-Host "Converted to: $pdf"
```

### Scheduled Task

```powershell
# Create scheduled task to convert documents daily
$trigger = New-ScheduledTaskTrigger -Daily -At 2:00AM
$action = New-ScheduledTaskAction -Execute "C:\ConvertDocuments.ps1"
Register-ScheduledTask -TaskName "DailyDocumentConversion" -Trigger $trigger -Action $action
```

## Performance Optimization

### Conversion Time

Typical times per operation:

| Operation | Time |
|-----------|------|
| File upload | <1s |
| Conversion | 1-10s |
| File download | <1s |
| **Total** | **2-15s** |

**Factors affecting speed**:
- File size (larger = slower)
- Format complexity
- System resource availability
- Network latency

### Resource Usage

**Memory**:
- Console app: ~50MB
- Per conversion: minimal additional memory

**CPU**:
- Conversion is CPU-intensive on OnlyOffice side
- Console app uses minimal CPU

**Network**:
- File upload: depends on file size
- Conversion request: <1KB
- File download: depends on output file size

### Parallel Conversions

```powershell
# Process multiple files in parallel
$files = Get-ChildItem *.docx

$files | ForEach-Object -Parallel {
    $appPath = ".\bin\Release\net8.0\OnlyOfficeConsoleApp.exe"
    & $appPath $_.FullName
} -ThrottleLimit 5  # Max 5 concurrent conversions
```

## Troubleshooting

### Enable Debug Logging

Modify `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### Check Kubernetes Services

```powershell
# Verify OnlyOffice is accessible
kubectl port-forward svc/onlyoffice-onlyoffice-documentserver 8080:80 -n onlyoffice

# Verify file-server is accessible
kubectl port-forward svc/onlyoffice-onlyoffice-documentserver-fileserver 9000:9000 -n onlyoffice

# Test endpoints
curl http://localhost:8080
curl http://localhost:9000/health
```

### View Application Logs

```powershell
# Build with debugging
dotnet build

# Run with console output
dotnet run -- document.docx

# Check for error messages and stack traces
```

### Test File Upload

```powershell
# Upload test file directly to file-server
$filePath = "C:\test.docx"
$uri = "http://localhost:9000/upload?filename=test.docx"

curl -X POST -F "file=@$filePath" $uri
```

## Advanced Configuration

### Custom JWT Secret

Update `appsettings.json`:

```json
{
  "OnlyOffice": {
    "JwtSecret": "your-super-secret-key-with-32-characters"
  }
}
```

Must match the JWT secret configured in:
```powershell
kubectl get secret jwt-secret-onlyoffice -n onlyoffice -o yaml
```

### Remote OnlyOffice Server

```powershell
# Configure for remote production server
.\OnlyOfficeConsoleApp.exe `
  document.docx `
  https://onlyoffice.production.com `
  "production-jwt-secret" `
  https://file-server.production.com
```

### Rate Limiting

For batch processing with rate limiting:

```powershell
# Convert with 5-second delay between files
Get-ChildItem *.docx | ForEach-Object {
    .\OnlyOfficeConsoleApp.exe $_.FullName
    Start-Sleep -Seconds 5  # Wait 5 seconds
}
```

## Best Practices

### 1. Verify Setup Before Bulk Operations

```powershell
# Test single file first
.\OnlyOfficeConsoleApp.exe test.docx

# Verify conversion succeeded
Test-Path test.pdf
```

### 2. Handle Errors Gracefully

```powershell
# Always check exit code
.\OnlyOfficeConsoleApp.exe $file
if ($LASTEXITCODE -ne 0) {
    # Log error
    # Retry or skip
    # Notify user
}
```

### 3. Keep Logs

```powershell
# Log all conversions
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
.\OnlyOfficeConsoleApp.exe document.docx | Tee-Object "conversion_$timestamp.log"
```

### 4. Monitor Resource Usage

```powershell
# Track conversion times
Measure-Command {
    .\OnlyOfficeConsoleApp.exe document.docx
} | Select-Object @{N="Time(s)";E={$_.TotalSeconds}}
```

### 5. Security

```powershell
# Don't hardcode secrets in scripts
# Use environment variables or parameter files
$secret = $env:ONLYOFFICE_JWT_SECRET
.\OnlyOfficeConsoleApp.exe document.docx http://localhost:8080 $secret
```

## Command Reference

```powershell
# Build commands
dotnet build                                    # Debug build
dotnet build -c Release                        # Release build
dotnet publish -c Release -r win-x64 --self-contained  # Standalone

# Run commands
dotnet run -- file.docx                        # From source
.\bin\Release\net8.0\OnlyOfficeConsoleApp.exe file.docx  # From binary

# Configuration
.\OnlyOfficeConsoleApp.exe file.docx http://server:8080 "secret" http://fileserver:9000

# Exit codes
$LASTEXITCODE                                  # Check last exit code
if ($? -eq $true) { ... }                     # Check success
```

## Summary

The console application provides:
- ✅ Simple command-line interface
- ✅ Automatic format detection
- ✅ JWT authentication
- ✅ Error handling
- ✅ Performance metrics
- ✅ Integration-friendly exit codes
- ✅ Batch processing support

For document conversion needs, use:
```powershell
.\OnlyOfficeConsoleApp.exe <input-file> [onlyoffice-url]
```

Output file is automatically created in the same directory with the converted format.
