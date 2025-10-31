# OnlyOffice Document Server - Architecture

A complete proof-of-concept project demonstrating OnlyOffice Document Server deployment on Kubernetes with .NET Core console application integration for automated document conversion.

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Kubernetes Cluster (microK8s)                                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌────────────────────────────────────────────────────────┐   │
│  │ OnlyOffice Document Server Pod (Multi-Replica)        │   │
│  │  ├─ OnlyOffice Container (HTTP: 80)                   │   │
│  │  │  - Document conversion engine                      │   │
│  │  │  - REST API for batch conversion                   │   │
│  │  │  - JWT token authentication                        │   │
│  │  │  - Memory: 2-4GB per replica                       │   │
│  │  │                                                     │   │
│  │  └─ File-Server Sidecar (HTTP: 9000)                  │   │
│  │     - Python file upload/download server              │   │
│  │     - Shared storage mount                            │   │
│  │     - Memory: 256MB per replica                       │   │
│  │                                                        │   │
│  │  Shared Volume: PersistentVolumeClaim (5Gi)           │   │
│  │    └─ /var/lib/onlyoffice-storage                     │   │
│  │       - File uploads location                         │   │
│  │       - Accessible to all replicas                    │   │
│  └────────────────────────────────────────────────────────┘   │
│           ↓ connects to                                         │
│  ┌────────────────────────────────────────────────────────┐   │
│  │ PostgreSQL Pod (StatefulSet)                           │   │
│  │  - Database for OnlyOffice metadata                    │   │
│  │  - Document history and settings                       │   │
│  │  - PersistentVolume: database storage                  │   │
│  └────────────────────────────────────────────────────────┘   │
│           ↓ connects to                                         │
│  ┌────────────────────────────────────────────────────────┐   │
│  │ RabbitMQ Pod (Message Queue)                           │   │
│  │  - Distributed document conversion queue              │   │
│  │  - Task distribution across replicas                  │   │
│  └────────────────────────────────────────────────────────┘   │
│                                                                 │
│  Kubernetes Services:                                          │
│  ├─ onlyoffice-documentserver:80 (ClusterIP)                  │
│  │  └─ Routes to OnlyOffice containers                        │
│  ├─ onlyoffice-documentserver-fileserver:9000 (ClusterIP)     │
│  │  └─ Routes to File-Server sidecars                         │
│  ├─ PostgreSQL:5432 (HeadlessService for StatefulSet)         │
│  └─ RabbitMQ:5672 (ClusterIP)                                 │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
         ↑                          ↑
         │ HTTP API (/converter)    │
         │                          │
         │                    Port Forward
         │                    (local testing)
         │                          │
         │                    kubectl port-forward
         │                    8080:80 & 9000:9000
         │                          │
   ┌─────┴──────────┬───────────────┘
   │                │
   V                V
Localhost      Kubernetes Service
(port 8080)    (production)
   │                │
   └─────┬──────────┘
         │
         V
  Console Application
  (OnlyOfficeConverter.cs)
  ├─ File Upload (POST to file-server:9000)
  ├─ Conversion Request (POST to /converter)
  │  └─ JWT Token generation & validation
  └─ File Download (GET from conversion response)
```

## Component Overview

### 1. OnlyOffice Document Server
**Purpose**: Core document conversion engine

**Capabilities**:
- Batch document conversion (20+ formats supported)
- DOCX ↔ PDF, XLSX ↔ PDF, PPTX ↔ PDF, etc.
- REST API endpoint: `/converter`
- JWT token authentication
- Horizontal scaling with multiple replicas
- Stateless design (converted files cached separately)

**Configuration**:
- Port: 80 (internal), 8080 (local port-forward)
- Environment variables for DB connection, JWT secret
- Health check: `/health` endpoint
- Replicas: 2-5 (configurable via Helm values)

### 2. File-Server Sidecar
**Purpose**: Handle file uploads and downloads for conversion jobs

**Responsibilities**:
- Accept file uploads via HTTP POST
- Store files in shared PersistentVolume
- Make files accessible to OnlyOffice conversion engine
- Serve converted files for download

**Implementation**:
- Python 3.11 application
- Flask micro-framework
- Running on port 9000 (internal), 9000 (port-forward)
- Mounted at same path as OnlyOffice (/var/lib/onlyoffice-storage)

**Endpoints**:
```
POST   /upload             - Upload file (form-data)
GET    /files/{filename}   - Download file
GET    /health             - Health check
```

### 3. PostgreSQL Database
**Purpose**: Persistence layer for OnlyOffice metadata

**Data stored**:
- Document metadata
- Conversion history
- User settings
- Cache configurations

**Deployment**:
- StatefulSet for pod identity and persistent naming
- PersistentVolume for database storage
- Service: postgres-service (headless for StatefulSet)
- Port: 5432

### 4. RabbitMQ Message Queue
**Purpose**: Distributed task queue for conversion jobs

**Responsibilities**:
- Queue conversion requests
- Distribute tasks to available workers
- Enable asynchronous processing
- Support high-concurrency scenarios

**Port**: 5672 (AMQP protocol)

### 5. .NET Console Application
**Purpose**: Client orchestrator for document conversion

**Workflow**:
```
1. Parse command-line arguments
   ├─ Input file path
   ├─ OnlyOffice URL (default: http://localhost:8080)
   ├─ JWT secret (from appsettings.json)
   └─ Output format (derived from extension)

2. Upload file to file-server
   ├─ HTTP POST to http://127.0.0.1:9000/upload
   ├─ Query parameters: filename, jwt_secret
   └─ Response: file uploaded confirmation

3. Prepare conversion request
   ├─ Generate JWT token with payload:
   │  ├─ filetype (input format)
   │  ├─ key (unique conversion ID)
   │  ├─ outputtype (desired output format)
   │  ├─ title (document title)
   │  └─ url (file location on file-server)
   └─ Token signed with HMAC-SHA256

4. Send conversion request to OnlyOffice
   ├─ HTTP POST to http://localhost:8080/converter
   ├─ JSON payload with JWT token
   ├─ Accept: application/json header
   └─ Response: conversion status with file URL

5. Download converted file
   ├─ Extract fileUrl from response
   ├─ HTTP GET to OnlyOffice cache endpoint
   └─ Save as output file (e.g., input.pdf)

6. Report results
   ├─ File size and duration
   ├─ Exit code 0 for success
   └─ Exit code non-zero for failure
```

**Key Features**:
- Automatic output format detection
- JWT token generation per request
- Error handling with detailed messages
- Performance reporting (time elapsed)
- Configurable via appsettings.json

## Data Flow

### Successful Conversion Flow

```
1. User Input
   ↓
   ./OnlyOfficeConsoleApp.exe document.docx http://localhost:8080
   ↓

2. File Upload Phase
   ├─ Read file from disk → document.docx (14,290 bytes)
   ├─ POST to http://127.0.0.1:9000/upload
   │  └─ Headers: JWT token in query params
   ├─ File-server receives → stores in /var/lib/onlyoffice-storage
   └─ Return: 200 OK

3. Conversion Request Phase
   ├─ Generate JWT payload:
   │  {
   │    "filetype": "docx",
   │    "key": "uuid-123",
   │    "outputtype": "pdf",
   │    "title": "document",
   │    "url": "http://127.0.0.1:9000/files/document.docx"
   │  }
   ├─ Sign with HMAC-SHA256
   ├─ POST to http://localhost:8080/converter
   │  └─ Body: { "filetype": "docx", "key": "uuid-123", ... "token": "..." }
   └─ Receive: 200 OK

4. OnlyOffice Processing
   ├─ Retrieve file from file-server via URL
   ├─ Convert DOCX → PDF internally
   ├─ Cache converted file: /onlyoffice/cache/files/...
   └─ Return: fileUrl to cached output

5. File Download Phase
   ├─ Extract fileUrl from response
   ├─ GET from http://localhost:8080/cache/files/...
   ├─ Download converted PDF (27,511 bytes)
   └─ Save as document.pdf

6. Success Report
   └─ ✓ Conversion completed successfully!
      - Converted file size: 27,511 bytes
      - Time elapsed: 1,385 ms
      - Output file: C:\...\document.pdf
```

## Storage Architecture

### Shared PersistentVolume

**Configuration**:
```yaml
PersistentVolumeClaim:
  name: onlyoffice-onlyoffice-documentserver-files-pvc
  storage: 5Gi
  accessMode: ReadWriteOnce
  storageClass: microk8s-hostpath

Mount Point:
  - OnlyOffice: /var/lib/onlyoffice-storage
  - File-Server: /var/lib/onlyoffice-storage
```

**Purpose**:
- Single source of truth for uploaded files
- Accessible to all pod replicas
- Survives pod restarts
- Ensures file consistency across cluster

**Why not emptyDir?**
- emptyDir volumes are ephemeral per pod
- Not shared across multiple replicas
- Lost on pod termination
- Not suitable for multi-instance deployments

## Security Implementation

### JWT Authentication

**Token Structure**:
```
Header: { "alg": "HS256", "typ": "JWT" }

Payload: {
  "filetype": "docx",
  "key": "unique-conversion-id",
  "outputtype": "pdf",
  "title": "document-title",
  "url": "http://file-server/path/to/file"
}

Signature: HMAC-SHA256(Header.Payload, jwt-secret)
```

**Validation**:
- OnlyOffice verifies signature on each request
- Prevents unauthorized conversion requests
- Ensures request parameters haven't been tampered
- Secret stored in Kubernetes Secret

### Secret Management

```yaml
Kubernetes Secret: jwt-secret-onlyoffice
  key: secret
  value: "your-jwt-secret-key-change-me" (configurable)

Kubernetes Secret: db-credentials
  username: onlyoffice_user
  password: (auto-generated)

Console App Configuration: appsettings.json
  key: JwtSecret
  value: matches Kubernetes Secret
```

**Best Practices**:
- Change default JWT secret in production
- Use strong random values (32+ characters)
- Rotate secrets periodically
- Store in secure Kubernetes Secrets, not ConfigMaps
- Never commit secrets to version control

## Scaling & High Availability

### Horizontal Scaling

**Configuration**:
```yaml
replicas: 3           # Deployed instances
minReplicas: 2        # Autoscaling minimum
maxReplicas: 5        # Autoscaling maximum
targetCpuUtilization: 70%
```

**How it works**:
1. Kubernetes Deployment manages multiple identical pods
2. Service load-balances across all running pods
3. All pods connect to same PostgreSQL and RabbitMQ
4. All pods access same PersistentVolume for files
5. HorizontalPodAutoscaler scales based on CPU usage

**Benefits**:
- No single point of failure
- Automatic failover on pod crash
- Load distribution across replicas
- Automatic scaling on high demand
- Zero-downtime rolling updates

### Pod Disruption Budget

Ensures at least 1 pod remains available during:
- Node maintenance
- Cluster upgrades
- Voluntary terminations

```yaml
minAvailable: 1  # Always keep 1 pod running
```

## Performance Characteristics

### Conversion Performance

**Typical metrics**:
- **Startup latency**: <100ms (console app load)
- **File upload**: <1s (small files)
- **Conversion time**: 1-10s per document
- **File download**: <1s (small files)
- **Total end-to-end**: 2-15s

**Factors affecting performance**:
- File size (larger files take longer)
- File format complexity
- Output format complexity
- System resource availability
- Number of concurrent conversions

### Resource Usage

**Per OnlyOffice Pod**:
- Memory: 2-4 GB
- CPU: 1-2 cores
- Storage: 50GB+ for cache

**Per File-Server Sidecar**:
- Memory: 256 MB
- CPU: 0.1 cores
- Storage: Shared with OnlyOffice

**Per PostgreSQL Pod**:
- Memory: 256 MB
- CPU: 0.1 cores
- Storage: 10GB+ for metadata

**Per RabbitMQ Pod**:
- Memory: 256 MB
- CPU: 0.1 cores

## Production Considerations

### Database Recommendations

For production, use external PostgreSQL:
- Managed database service (AWS RDS, Azure Database)
- Separate from Kubernetes cluster
- Automated backups and failover
- Better resource isolation

Configuration:
```yaml
# Change from local StatefulSet to external connection
env:
  DB_HOST: my-postgres-instance.c.us-central1.rds.amazonaws.com
  DB_PORT: 5432
  DB_NAME: onlyoffice_prod
  DB_USER: (from Kubernetes Secret)
  DB_PWD: (from Kubernetes Secret)
```

### Monitoring & Logging

**Recommended setup**:
- Prometheus for metrics collection
- Loki or ELK for log aggregation
- Grafana dashboards for visualization
- AlertManager for incident notifications

**Key metrics to monitor**:
- Conversion success rate
- Average conversion time
- Pod CPU and memory usage
- OnlyOffice error rates
- Database connection pool status

### Backup Strategy

**What to backup**:
- PostgreSQL database (metadata, history)
- Converted file cache (optional, can be regenerated)
- Kubernetes manifests and Helm values

**Backup frequency**:
- Database: Daily or per business requirements
- Manifests: After every change (version control)

## Deployment Environments

### Development (microK8s on Windows)
```yaml
replicas: 1
memory: 512Mi per container
storage: 1Gi
computeResources: Minimal
networking: localhost port-forward
```

### Testing (Kubernetes cluster)
```yaml
replicas: 2
memory: 2Gi per OnlyOffice pod
storage: 10Gi
computeResources: Standard
networking: ClusterIP services
```

### Production (Production Kubernetes)
```yaml
replicas: 3-5 (with autoscaling)
memory: 4Gi per OnlyOffice pod
storage: 50Gi+
computeResources: Reserved/limited
networking: Ingress + LoadBalancer
SSL/TLS: Enabled via Ingress
```

## Technology Stack

| Component | Technology | Version | Purpose |
|-----------|-----------|---------|---------|
| Container Orchestration | Kubernetes | 1.20+ | Pod management |
| Package Manager | Helm | 3.0+ | Kubernetes deployment |
| Document Server | OnlyOffice | 8.0.1 | Document conversion |
| Database | PostgreSQL | 12+ | Metadata storage |
| Message Queue | RabbitMQ | 3.9+ | Task distribution |
| Client App | .NET Core | 8.0 | Conversion orchestration |
| File Server | Python | 3.11 | File upload/download |
| Local K8s | microK8s | 1.20+ | Development environment |
| Shell | PowerShell | 5.1+ | Windows command line |

## Licensing

**OnlyOffice Document Server**:
- License: AGPL v3
- Cost: FREE for self-hosted deployments
- Restrictions: Must share modifications (AGPL)
- No per-user licensing
- No conversion limits

**Optional Commercial Licenses**:
- Enterprise Edition: ~$1,500/year
- Developer Edition: Custom pricing
- Cloud Service: Starting at $5/month per user

For this POC, the free AGPL v3 license is suitable for internal deployments.

## Supported Conversions

### Implemented & Tested

**✅ DOCX → PDF**
- Input: .docx, .doc, .rtf, .odt, .txt
- Output: .pdf
- Status: Tested and working
- Speed: ~1.4 seconds

**✅ PDF → DOCX** (NEW)
- Input: .pdf
- Output: .docx (editable format)
- Status: Tested and working
- Speed: ~1.5 seconds
- Note: Some formatting may be lost (lossy conversion)

### Easily Extensible

The same generic conversion API supports any-to-any format conversion:
- XLSX → PDF (Excel to PDF)
- PPTX → PDF (PowerPoint to PDF)
- ODP → PDF (OpenDocument Presentation to PDF)
- PDF → XLSX (PDF to Excel)
- And many more (see OnlyOffice API documentation for full list)

To add new conversions, simply update the `DetermineOutputFormat()` method in `Program.cs`:

```csharp
static string DetermineOutputFormat(string inputExtension)
{
    return inputExtension switch
    {
        ".pdf" => "docx",           // PDF to DOCX
        ".xlsx" => "pdf",           // Excel to PDF
        ".pptx" => "pdf",           // PowerPoint to PDF
        _ => "pdf"                  // Default to PDF
    };
}
```

## Summary

This architecture provides:
- ✅ Scalable document conversion
- ✅ Bidirectional conversions (DOCX ↔ PDF)
- ✅ High availability with multiple replicas
- ✅ Persistent file storage
- ✅ JWT security
- ✅ Kubernetes-native deployment
- ✅ Production-ready configuration options
- ✅ Easy integration via console app
- ✅ Extensible for any format combination
- ✅ Zero licensing costs (AGPL v3)

All components work together to create a robust, scalable document conversion platform ready for both development and production environments.
