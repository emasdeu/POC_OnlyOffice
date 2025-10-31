# OnlyOffice Storage Server

A lightweight HTTP file server designed to run as a sidecar container in the OnlyOffice Document Server pod. It handles file uploads and downloads for the document conversion process.

## Features

- **Simple HTTP API** for file upload/download
- **Shared volume integration** with OnlyOffice container
- **Security checks** to prevent directory traversal attacks
- **Proper MIME types** for different file formats
- **Health check endpoint** for Kubernetes liveness probes
- **Configurable** via environment variables
- **Lightweight** - runs on minimal resources

## Endpoints

### Health Check
```
GET /health
```
Returns: `{ "status": "healthy" }`

### Server Info
```
GET /
```
Returns server information and storage path.

### Upload File
```
POST /upload
Content-Type: multipart/form-data

file: <binary file content>
```

**Response (Success - 200):**
```json
{
  "fileUrl": "http://localhost:8000/files/document.docx",
  "filename": "document.docx"
}
```

**Response (Error - 400/500):**
```json
{
  "error": "Error description"
}
```

### Download File
```
GET /files/{filename}
```

**Response:**
- Status 200: File content with proper MIME type
- Status 404: File not found
- Status 403: Invalid filename (path traversal attempt)

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `STORAGE_PATH` | `/var/lib/onlyoffice-storage` | Directory where files are stored |
| `LISTEN_PORT` | `8000` | Port to listen on |

## Building Docker Image

```bash
cd OnlyOfficeStorageServer
docker build -t onlyoffice-storage-server:1.0 .
```

## Running Standalone (for testing)

```bash
dotnet run
```

Starts server on `http://localhost:8000`

## Kubernetes Integration

### As Sidecar in OnlyOffice Pod

The Helm chart deployment.yaml includes this server as a sidecar container.

Configuration in `values.yaml`:
```yaml
fileServer:
  enabled: true
  port: 8000
  storageMount: /var/lib/onlyoffice-storage
  image:
    repository: onlyoffice-storage-server
    tag: "1.0"
```

### Access from Console App

From outside cluster (via port-forward):
```bash
POST http://localhost:8000/upload
GET http://localhost:8000/files/document.docx
```

### Access from OnlyOffice (same pod)

OnlyOffice references files:
```json
{
  "url": "http://localhost:8000/files/document.docx"
}
```

## Testing

### Upload File
```bash
curl -X POST -F "file=@test.docx" http://localhost:8000/upload
```

### Download File
```bash
curl http://localhost:8000/files/test.docx -o downloaded.docx
```

### Check Health
```bash
curl http://localhost:8000/health
```

## File Storage

Files are stored in the directory specified by `STORAGE_PATH`. In the pod, this is typically:
```
/var/lib/onlyoffice-storage/
```

This directory is shared between the storage server container and the OnlyOffice container via a shared volume mount.

## Security

- **Path traversal prevention**: Filenames are validated and normalized
- **Filename sanitization**: Only the filename component is accepted, paths are rejected
- **MIME type validation**: Proper Content-Type headers for served files
- **No authentication**: Designed for internal cluster use only (same pod)

## Notes

- Files are stored in plain format (not encrypted)
- Old files should be cleaned up periodically by the application
- The storage volume should have sufficient space for document conversions
- For production, consider adding:
  - File expiration/cleanup policies
  - File size limits
  - Access logging
  - Authentication/authorization

