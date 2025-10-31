# POC_OnlyOffice - Document Conversion Pipeline with Kubernetes

A proof-of-concept demonstration of OnlyOffice Document Server deployed in Kubernetes with a custom document conversion pipeline, featuring bidirectional PDF↔DOCX conversion capabilities.

## 🎯 Project Overview

This project showcases how to:
- Deploy OnlyOffice Document Server on Kubernetes (microK8s)
- Build a document conversion pipeline (.NET 8 Console App)
- Implement local file upload/download without external cloud storage
- Configure JWT authentication for the OnlyOffice Conversion API
- Manage cache expiration and cleanup policies
- Test end-to-end document conversions

**Perfect for:** Learning, development, POC testing, and microservices integration.

---

## 📁 Project Structure

```
POC_OnlyOffice/
├── README.md                           # This file
├── OnlyOffice_Overview.md              # High-level overview
│
├── docs/                               # Documentation folder
│   ├── ARCHITECTURE.md                 # System design and components
│   ├── KUBERNETES_SETUP.md             # K8s deployment guide
│   ├── CONSOLE_APP_USAGE.md            # CLI application reference
│   ├── TROUBLESHOOTING.md              # Issues and solutions
│   ├── CACHE_CONFIGURATION.md          # Cache management guide
│   ├── CACHE_CONFIGURATION_SUMMARY.md  # Quick cache reference
│   ├── PDF_TO_DOCX_CONVERSION.md       # Bidirectional conversion guide
│   └── COMPARISON_WITH_OFFICIAL_REPO.md # Official repo comparison
│
├── k8s/                                # Kubernetes configurations
│   ├── helm-chart/                     # Helm chart for OnlyOffice
│   │   └── onlyoffice/
│   │       ├── Chart.yaml
│   │       ├── values.yaml             # Deployment configuration
│   │       └── templates/              # K8s resource templates
│   │           ├── deployment.yaml
│   │           ├── service.yaml
│   │           ├── configmap.yaml
│   │           ├── pvc.yaml
│   │           └── ...
│   │
│   └── OnlyOfficeStorageServer/        # Custom file-server (Python)
│       ├── app.py                      # HTTP server for file uploads
│       ├── requirements.txt
│       └── Dockerfile
│
├── OnlyOfficeConsoleApp/               # .NET 8 Console Application
│   ├── Program.cs                      # Entry point
│   ├── OnlyOfficeConverter.cs          # Conversion logic
│   ├── OnlyOfficeConsoleApp.csproj     # Project file
│   ├── bin/
│   └── obj/
│
├── test_files/                         # Test documents
│   ├── test_document.docx
│   ├── test_document.pdf
│   ├── test_pdf_source.docx
│   ├── test_pdf_source.pdf
│   ├── SampleContract.pdf
│   └── SampleContract.docx
│
└── run-conversion-test.ps1             # Healthcheck script
```

---

## 🚀 Quick Start

### Prerequisites

- **microK8s** or Kubernetes cluster (1.24+)
- **kubectl** configured
- **.NET 8 SDK** (for console app compilation)
- **Helm 3** (for chart deployment)
- **PowerShell 7+** (for test scripts)

### 1. Deploy OnlyOffice to Kubernetes

```powershell
# Navigate to project root
cd c:\WK_SourceCode\POC_OnlyOffice

# Deploy using Helm
helm install onlyoffice k8s/helm-chart/onlyoffice -n onlyoffice --create-namespace

# Verify deployment
kubectl get pods -n onlyoffice
kubectl get svc -n onlyoffice
```

### 2. Set Up Port Forwards

```powershell
# OnlyOffice API (Port 8080)
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver 8080:80 &

# File-Server (Port 9000)
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver-fileserver 9000:9000 &
```

### 3. Run Healthcheck Test

```powershell
# Test PDF → DOCX conversion
.\run-conversion-test.ps1

# Expected output: "=== SUCCESS ==="
```

### 4. Use Console App for Conversions

```powershell
# Convert DOCX to PDF
.\OnlyOfficeConsoleApp\bin\Release\net8.0\OnlyOfficeConsoleApp.exe test_document.docx http://localhost:8080

# Convert PDF to DOCX
.\OnlyOfficeConsoleApp\bin\Release\net8.0\OnlyOfficeConsoleApp.exe test_pdf_source.pdf http://localhost:8080
```

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Windows Machine                           │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │        Console Application (.NET 8)                   │   │
│  │  - Document upload                                   │   │
│  │  - Conversion requests                               │   │
│  │  - Download converted files                          │   │
│  └──────┬───────────────────────────────┬───────────────┘   │
│         │ Port 9000                     │ Port 8080         │
│         │ (File Upload)                 │ (Conversion)      │
│         ▼                               ▼                    │
└─────────────────────────────────────────────────────────────┘
         │                                │
         ▼                                ▼
┌─────────────────────────────────────────────────────────────┐
│              microK8s Kubernetes Cluster                    │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Pod Replica 1:              Pod Replica 2:                │
│  ┌──────────────┐           ┌──────────────┐               │
│  │File-Server   │           │File-Server   │               │
│  │(Python)      │           │(Python)      │               │
│  │Port 9000     │           │Port 9000     │               │
│  └──────┬───────┘           └──────┬───────┘               │
│         │                         │                        │
│  ┌──────▼─────────────────────────▼──────────────────┐    │
│  │      OnlyOffice Document Server                   │    │
│  │  - Conversion Engine (/converter endpoint)        │    │
│  │  - JWT Authentication                             │    │
│  │  - PDF ↔ DOCX bidirectional conversion            │    │
│  │  - Port 8080                                      │    │
│  └──────┬──────────────────────────────────────┬─────┘    │
│         │                                      │           │
│  ┌──────▼────────────────────────────────────▼──────┐    │
│  │     Shared PVC Storage (50Gi)                    │    │
│  │  ├─ /upload/      (temp file uploads)           │    │
│  │  ├─ /cache/       (converted files, 30min TTL) │    │
│  │  └─ /data/        (persistent data)            │    │
│  └──────────────────────────────────────────────────┘    │
│         │              │              │                   │
│  ┌──────▼─────┬────────▼──────┬──────▼─────────┐         │
│  │ PostgreSQL │  RabbitMQ     │  (Future: Redis)│        │
│  │ (metadata) │  (messaging)  │  (caching)      │        │
│  └────────────┴───────────────┴─────────────────┘        │
│                                                            │
└─────────────────────────────────────────────────────────────┘
```

**Key Components:**
- **OnlyOffice Document Server** - Conversion engine
- **File-Server Sidecar** - Local HTTP server for file uploads
- **Shared PVC** - Document storage (50Gi)
- **PostgreSQL** - Metadata storage
- **RabbitMQ** - Task messaging
- **Helm Chart** - Kubernetes deployment automation

---

## 📚 Documentation

### Core Documentation

| Document | Purpose |
|----------|---------|
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | System design, components, data flow |
| [KUBERNETES_SETUP.md](docs/KUBERNETES_SETUP.md) | Step-by-step K8s deployment guide |
| [CONSOLE_APP_USAGE.md](docs/CONSOLE_APP_USAGE.md) | CLI application reference and examples |
| [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) | Common issues and solutions |

### Advanced Topics

| Document | Purpose |
|----------|---------|
| [CACHE_CONFIGURATION.md](docs/CACHE_CONFIGURATION.md) | Cache settings and expiration (30 minutes) |
| [CACHE_CONFIGURATION_SUMMARY.md](docs/CACHE_CONFIGURATION_SUMMARY.md) | Quick cache reference |
| [PDF_TO_DOCX_CONVERSION.md](docs/PDF_TO_DOCX_CONVERSION.md) | Bidirectional conversion details |
| [COMPARISON_WITH_OFFICIAL_REPO.md](docs/COMPARISON_WITH_OFFICIAL_REPO.md) | vs Official OnlyOffice repo |

---

## ✨ Key Features

### ✅ Implemented

- **PDF ↔ DOCX Bidirectional Conversion**
  - Test results: ~1.5 seconds per conversion
  - Tested with real documents (SampleContract.pdf)

- **Configurable Cache (30 Minutes)**
  - Automatic cleanup every 30 minutes
  - Prevents disk bloat in test environments
  - Configurable via Helm values

- **JWT Authentication**
  - Full request payload in token (security best practice)
  - Proper HS256 signature validation

- **Multi-Replica Deployment**
  - 2-5 replicas with HPA
  - Shared PVC for data consistency
  - Automatic pod restart on failure

- **Automated Testing**
  - PowerShell healthcheck script
  - End-to-end conversion validation
  - Port-forward management

- **Comprehensive Documentation**
  - Architecture diagrams
  - Troubleshooting guide with real issues
  - Cache management guide
  - Comparison with official repository

### 🔄 Architecture Innovation

**File-Server Sidecar Pattern** (Unique to this POC)
- Local HTTP server (Python) in each pod
- Enables file uploads without external storage
- Multi-replica support with shared PVC
- Perfect for development and testing

---

## 🧪 Testing

### Run Healthcheck Test

```powershell
# Default: PDF → DOCX (test_pdf_source.pdf)
.\run-conversion-test.ps1

# Custom file and format
.\run-conversion-test.ps1 -InputFile "test_document.docx" -OutputFormat "pdf"
```

### Test Results

```
Input:  test_pdf_source.pdf (27,511 bytes)
Output: test_pdf_source.docx (8,831 bytes)
Time:   ~1,495ms
Status: ✅ SUCCESS
```

### Manual Conversion

```powershell
# Convert any file in test_files/ folder
.\OnlyOfficeConsoleApp\bin\Release\net8.0\OnlyOfficeConsoleApp.exe <filename> http://localhost:8080
```

---

## 🔧 Configuration

### Cache Expiration (Default: 30 Minutes)

Edit `k8s/helm-chart/onlyoffice/values.yaml`:

```yaml
configMap:
  enabled: true
  onlyofficeConfig:
    storage:
      urlExpires: 1800      # 30 minutes
    expire:
      files: 1800           # 30 minutes
      filesCron: "*/30 * * * * *"  # Cleanup every 30 min
```

### JWT Secret

Edit `k8s/helm-chart/onlyoffice/values.yaml`:

```yaml
env:
  JWT_SECRET: "your-jwt-secret-key-change-me"
```

### Database Connection

Edit `k8s/helm-chart/onlyoffice/values.yaml`:

```yaml
postgresql:
  externalDatabase:
    host: "postgres.onlyoffice.svc.cluster.local"
    database: "onlyoffice"
    username: "onlyoffice"
    password: "onlyoffice-password"
```

---

## 📊 Supported Conversions

| Input Format | Output Format | Status | Test Result |
|--------------|---------------|--------|-------------|
| DOCX | PDF | ✅ Tested | 14,290 → 27,511 bytes |
| PDF | DOCX | ✅ Tested | 27,511 → 8,831 bytes |
| XLSX | PDF | ✅ Supported | Not tested |
| PPTX | PDF | ✅ Supported | Not tested |
| ODP | PDF | ✅ Supported | Not tested |

For full matrix, see [PDF_TO_DOCX_CONVERSION.md](docs/PDF_TO_DOCX_CONVERSION.md)

---

## 🔐 Security Considerations

### JWT Token

- ✅ HS256 signature with secret key
- ✅ Includes request parameters (not just timestamp)
- ✅ Configurable secret in values.yaml

### Network

- Optional Network Policy (disabled by default)
- Can be enabled in values.yaml

### Secrets

- Database password in environment variables
- JWT secret in environment variables
- Should use Kubernetes Secrets in production

### Production Recommendations

- [ ] Rotate JWT secret regularly
- [ ] Use Kubernetes Secrets for sensitive data
- [ ] Enable Network Policies
- [ ] Implement RBAC
- [ ] Use managed databases (RDS, Cloud SQL)
- [ ] Enable audit logging

---

## 🚀 Deployment Options

### Development (microK8s)

```powershell
helm install onlyoffice k8s/helm-chart/onlyoffice -n onlyoffice --create-namespace
```

### Production (Cloud K8s)

1. Use managed Kubernetes (EKS, GKE, AKS)
2. Configure external storage (S3, Azure Blob)
3. Use official OnlyOffice Kubernetes repository
4. Add Redis for caching
5. Add monitoring (Prometheus, Grafana)

For migration path, see [COMPARISON_WITH_OFFICIAL_REPO.md](docs/COMPARISON_WITH_OFFICIAL_REPO.md)

---

## 📈 Performance Characteristics

| Metric | Value | Notes |
|--------|-------|-------|
| **Conversion Time** | ~1.5 seconds | Per document conversion |
| **File Upload Time** | <100ms | Local network |
| **File Download Time** | <100ms | Local network |
| **Cache Expiration** | 30 minutes | Configurable |
| **Pod Startup Time** | ~15-20 seconds | First pod slower (downloads image) |
| **Replica Count** | 2-5 | Horizontally scalable |

---

## 🛠️ Development

### Building Console App

```powershell
# Build
dotnet build OnlyOfficeConsoleApp/OnlyOfficeConsoleApp.csproj

# Run
dotnet run --project OnlyOfficeConsoleApp test_document.docx

# Publish
dotnet publish -c Release OnlyOfficeConsoleApp/OnlyOfficeConsoleApp.csproj
```

### Building File-Server

```powershell
# Docker build
docker build -t file-server:latest k8s/OnlyOfficeStorageServer/

# Run locally
python k8s/OnlyOfficeStorageServer/app.py
```

### Modifying Helm Chart

```powershell
# Validate chart
helm lint k8s/helm-chart/onlyoffice

# Dry-run deployment
helm install onlyoffice k8s/helm-chart/onlyoffice -n onlyoffice --dry-run

# Upgrade deployment
helm upgrade onlyoffice k8s/helm-chart/onlyoffice -n onlyoffice
```

---

## 🐛 Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| Port 8080 already in use | Kill old port-forward: `Get-Process kubectl \| Stop-Process` |
| Pod CrashLoopBackOff | Check logs: `kubectl logs -n onlyoffice <pod-name>` |
| Conversion fails with 404 | Verify file-server is running: `curl localhost:9000/health` |
| JWT token invalid | Verify secret key matches in values.yaml |

For detailed troubleshooting, see [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)

---

## 📖 Additional Resources

- [Official OnlyOffice Kubernetes Docs](https://github.com/ONLYOFFICE/Kubernetes-Docs)
- [OnlyOffice Conversion API](https://api.onlyoffice.com/docs/docs-api/additional-api/conversion-api/)
- [Kubernetes Documentation](https://kubernetes.io/docs/)
- [Helm Documentation](https://helm.sh/docs/)

---

## 🎓 Learning Outcomes

After working through this project, you'll understand:

1. ✅ OnlyOffice Document Server architecture and APIs
2. ✅ Kubernetes deployment with Helm charts
3. ✅ JWT authentication and token validation
4. ✅ Sidecar container pattern in Kubernetes
5. ✅ PersistentVolume and PersistentVolumeClaim usage
6. ✅ Multi-replica deployment with shared storage
7. ✅ Cache management and expiration policies
8. ✅ Document conversion pipelines
9. ✅ Kubernetes troubleshooting and debugging
10. ✅ DevOps and infrastructure-as-code practices

---

## 📝 License

This project is provided as-is for educational and proof-of-concept purposes.

---

## 👤 Author

**emasdeu** - GitHub user

---

## 🤝 Contributing

Contributions are welcome! Areas for enhancement:

- [ ] Add more format conversions (XLSX, PPTX)
- [ ] Implement Redis caching layer
- [ ] Add Prometheus metrics
- [ ] Create Grafana dashboards
- [ ] Add integration tests
- [ ] Support batch conversions
- [ ] Add API endpoint wrapper

---

## 📞 Support

For issues or questions:

1. Check [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)
2. Review [ARCHITECTURE.md](docs/ARCHITECTURE.md)
3. Check Kubernetes pod logs
4. Review OnlyOffice official documentation

---

**Last Updated:** October 31, 2025  
**Status:** ✅ Production Ready for POC/Development
