# OnlyOffice Console Application

A .NET 8 console application for converting documents using OnlyOffice Document Server.

## Prerequisites

- **.NET 8 SDK** or later
- **OnlyOffice Document Server** instance running (locally or remote)
- **Windows PowerShell** or Command Prompt

## Building the Application

```powershell
cd .\OnlyOfficeConsoleApp
dotnet build -c Release
```

This will create an executable in `bin\Release\net8.0\OnlyOfficeConsoleApp.exe`

## Usage

### Basic Command Line

```powershell
.\OnlyOfficeConsoleApp.exe <input_file> [onlyoffice_url] [jwt_secret]
```

### Parameters

- **input_file** (required): Path to the document file to convert
- **onlyoffice_url** (optional): Base URL of OnlyOffice Document Server (default: `http://localhost`)
- **jwt_secret** (optional): JWT secret for authentication

### Examples

#### Convert DOCX to PDF (Local Server)
```powershell
.\OnlyOfficeConsoleApp.exe C:\documents\document.docx
```

#### Convert Using Custom Server URL
```powershell
.\OnlyOfficeConsoleApp.exe C:\documents\document.docx http://onlyoffice-server.com:8080
```

#### Convert With JWT Authentication
```powershell
.\OnlyOfficeConsoleApp.exe C:\documents\document.docx http://onlyoffice-server.com my-secret-key
```

#### Convert PDF to DOCX
```powershell
.\OnlyOfficeConsoleApp.exe C:\documents\document.pdf
```

### Output

The converted file will be saved in the same directory as the input file with the appropriate extension:
- Input: `document.docx` → Output: `document.pdf`
- Input: `document.xlsx` → Output: `document.pdf`
- Input: `document.pdf` → Output: `document.docx`

## Supported Input Formats

### Document Formats
- Microsoft Office: `.docx`, `.doc`, `.xlsx`, `.xls`, `.pptx`, `.ppt`
- OpenDocument: `.odt`, `.ods`, `.odp`
- Other: `.txt`, `.rtf`, `.csv`, `.pdf`

## Configuration

### Environment Variables (Optional)
You can set environment variables to avoid command-line arguments:

```powershell
$env:ONLYOFFICE_URL = "http://onlyoffice-server.com"
$env:JWT_SECRET = "your-secret-key"
```

## Running OnlyOffice Locally

### Using Docker
```powershell
docker run -d -p 80:80 onlyoffice/documentserver:latest
```

Then run the converter:
```powershell
.\OnlyOfficeConsoleApp.exe C:\documents\document.docx http://localhost
```

### Using Docker Compose
See the `helm-chart` folder for Kubernetes deployment options.

## Error Handling

The application returns exit codes for scripting:
- **0**: Success
- **1**: Error (file not found, conversion failed, etc.)

Check console output for detailed error messages.

## Architecture

### Classes

- **OnlyOfficeConverter**: Main converter class
  - `ConvertDocumentAsync(filePath, outputFormat)`: Converts a local file
  - `ConvertDocumentAsync(bytes, fileName, outputFormat)`: Converts from byte array
  - Handles file upload, JWT token generation, and conversion requests

- **Program**: Console entry point
  - Parses command-line arguments
  - Validates input files
  - Displays progress and results

### Request/Response Models

- **ConversionRequest**: Payload for OnlyOffice ConvertService.ashx endpoint
- **ConversionResponse**: Response from conversion service
- **UploadResponse**: Response from file upload service

## Performance Considerations

- **Network latency**: Depends on OnlyOffice server location
- **File size**: Large files may take longer to process
- **Server resources**: OnlyOffice server needs adequate CPU/memory
- **Typical conversion time**: 1-10 seconds for average documents

## JWT Authentication

If your OnlyOffice server requires JWT tokens:
1. Provide the JWT secret as the third parameter
2. The application automatically generates and signs JWT tokens
3. Uses HMAC-SHA256 algorithm

## Troubleshooting

### Connection Issues
```
Error: Unable to connect to http://localhost
```
- Verify OnlyOffice server is running
- Check network connectivity
- Use `http://localhost:8080` if running on non-standard port

### File Not Found
```
Error: Input file not found: C:\documents\document.docx
```
- Verify the file path is correct
- Check file permissions
- Use absolute paths

### Conversion Failed
```
Error: Conversion failed with error code: -1
```
- Check OnlyOffice server logs
- Verify file format is supported
- Ensure sufficient server resources

## Development

### Project Structure
```
OnlyOfficeConsoleApp/
├── OnlyOfficeConsoleApp.csproj    # Project file
├── Program.cs                      # Entry point
├── OnlyOfficeConverter.cs           # Converter implementation
├── README.md                        # This file
└── .gitignore                       # Git ignore rules
```

### Building for Distribution

```powershell
# Release build
dotnet publish -c Release -r win-x64 --self-contained

# Output will be in bin\Release\net8.0\win-x64\publish\
```

## License

This application uses OnlyOffice Document Server (AGPL v3 - free for self-hosted deployments).

See https://github.com/ONLYOFFICE/DocumentServer for more information.
