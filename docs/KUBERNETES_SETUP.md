# Kubernetes & Docker Setup Guide

Complete instructions for deploying OnlyOffice Document Server to Kubernetes using Helm.

## Prerequisites

### Required Software

Ensure the following tools are installed and configured:

- **microK8s** (1.20+)
- **Helm** (3.0+)
- **kubectl** (1.20+)
- **.NET 8 SDK**

### Installation Guide

For detailed instructions on installing and configuring a local microK8s environment on Windows, refer to:

📚 **[Entorno local en K8s MicroK8s](https://confluence.wolterskluwer.io/spaces/TAASDO/pages/696502704/Entorno+local+en+K8s+Microk8s)**

This guide covers:
- microK8s installation and setup
- Helm installation and configuration
- kubectl configuration
- Storage and add-ons enablement
- Troubleshooting common setup issues

### Configure kubectl Context

```powershell
# microK8s should be default context, verify:
kubectl config current-context
# Should show: microk8s

# If not, set it:
kubectl config use-context microk8s

# List all contexts
kubectl config get-contexts
```

## Namespace Setup

### Create OnlyOffice Namespace

```powershell
# Create dedicated namespace
kubectl create namespace onlyoffice

# Verify creation
kubectl get namespace onlyoffice

# Set as default namespace (optional)
kubectl config set-context --current --namespace=onlyoffice
```

### Verify Namespace

```powershell
# Check namespace details
kubectl describe namespace onlyoffice

# Should show:
# Name:         onlyoffice
# Status:       Active
```

## Helm Chart Deployment

### Chart Structure

The Helm chart (`k8s/helm-chart/onlyoffice/`) includes the following components:

```
helm-chart/onlyoffice/
├── Chart.yaml                    # Helm chart metadata
├── values.yaml                   # Default configuration values
├── templates/
│   ├── deployment.yaml           # OnlyOffice + Python File-Server sidecar
│   ├── service.yaml              # Service definitions
│   ├── configmap.yaml            # Configuration overrides
│   ├── fileserver-configmap.yaml # Python file-server script (used, .NET server not included)
│   ├── pvc.yaml                  # Storage configuration
│   └── ...
```

### Components Deployed

1. **OnlyOffice Document Server Container**
   - Main conversion engine (port 80)
   - Runs the full OnlyOffice service stack
   - 2 replicas by default with HPA (2-5 max)

2. **File-Server Sidecar Container** 
  - Lightweight Python HTTP server (port 9000)
  - Handles file uploads/downloads for conversions
  - Shared storage volume with OnlyOffice container
  - Runs in the same pod as OnlyOffice
  - **Note:** The .NET OnlyOfficeStorageServer project is NOT used. The Python file-server is the only supported and deployed file-server in this setup.

3. **PostgreSQL** (External)
   - Database for document coordination
   - Located in `local-hcm-system` namespace
   - Shared cluster resource

4. **Storage Volume**
   - Shared PersistentVolumeClaim (50Gi)
   - Mounts at `/var/lib/onlyoffice-storage`
   - Persists across pod restarts

### Install Helm Chart

```powershell
# Basic installation with default values
helm install onlyoffice ./k8s/helm-chart/onlyoffice `
  -n onlyoffice `
  --create-namespace

# Installation complete when shown
# NOTES section in helm output
```

### Verify Helm Installation

```powershell
# List installed Helm releases
helm list -n onlyoffice

# Check release status
helm status onlyoffice -n onlyoffice

# Show release values
helm get values onlyoffice -n onlyoffice
```

### Custom Configuration

```powershell
# Install with custom values
helm install onlyoffice ./helm-chart/onlyoffice `
  -n onlyoffice `
  --set replicaCount=3 `
  --set persistence.size=10Gi

# Or use a values file
helm install onlyoffice ./helm-chart/onlyoffice `
  -n onlyoffice `
  -f custom-values.yaml
```

## Pod Deployment Verification

### Check Pod Status

```powershell
# Watch pods starting
kubectl get pods -n onlyoffice -w

# Expected output (wait 30-60 seconds):
# NAME                                             READY   STATUS    RESTARTS
# onlyoffice-onlyoffice-documentserver-0          0/2     Pending   0
# onlyoffice-onlyoffice-documentserver-0          1/2     Running   0
# onlyoffice-onlyoffice-documentserver-0          2/2     Running   0
# onlyoffice-postgresql-0                         1/1     Running   0
# onlyoffice-rabbitmq-0                           1/1     Running   0
```

### Pod Details

```powershell
# Describe a pod
kubectl describe pod -n onlyoffice <pod-name>

# View pod logs
kubectl logs -n onlyoffice <pod-name>

# View specific container logs
kubectl logs -n onlyoffice <pod-name> -c onlyoffice

# View file-server sidecar logs
kubectl logs -n onlyoffice <pod-name> -c file-server

# Follow logs (tail -f)
kubectl logs -n onlyoffice <pod-name> -f
```

### All Resources

```powershell
# List all resources in namespace
kubectl get all -n onlyoffice

# Show deployments
kubectl get deployment -n onlyoffice

# Show services
kubectl get svc -n onlyoffice

# Show persistent volumes
kubectl get pv,pvc -n onlyoffice

# Show secrets
kubectl get secret -n onlyoffice
```

## Service Access Configuration

### Port Forwarding (Development)

```powershell
# Forward OnlyOffice port (run in separate terminal)
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver 8080:80

# In another terminal, forward file-server port
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver-fileserver 9000:9000

# Now accessible at:
# - OnlyOffice: http://localhost:8080
# - File-Server: http://localhost:9000
```

### Service Discovery (Kubernetes Internal)

Within the cluster, use DNS names:

```
# OnlyOffice service (port 80)
http://onlyoffice-onlyoffice-documentserver.onlyoffice.svc.cluster.local

# File-server service (port 9000)
http://onlyoffice-onlyoffice-documentserver-fileserver.onlyoffice.svc.cluster.local

# PostgreSQL service
postgres-service.onlyoffice.svc.cluster.local:5432

# RabbitMQ service
onlyoffice-rabbitmq.onlyoffice.svc.cluster.local:5672
```

### Load Balancer (Production)

```yaml
# Modify service type in values.yaml or override at install:
helm install onlyoffice ./helm-chart/onlyoffice \
  -n onlyoffice \
  --set service.type=LoadBalancer

# Get external IP
kubectl get svc -n onlyoffice
```

## File-Server Sidecar Configuration

The File-Server is a lightweight Python HTTP server that runs as a **sidecar container** in the OnlyOffice pod. It handles file uploads and downloads for the document conversion process.

**Note:** The .NET OnlyOfficeStorageServer project is not included or required. All file upload/download operations are handled by the Python file-server sidecar defined in the Helm chart.

### Why File-Server?

- **Local file access**: OnlyOffice needs to access files for conversion
- **Sidecar pattern**: Runs in the same pod, shares storage volume
- **HTTP interface**: Simple REST API for file operations
- **Minimal overhead**: Lightweight Python server using ~128Mi memory

### File-Server Architecture

```
┌─────────────────────────────────────────┐
│       OnlyOffice Pod                    │
├─────────────────────────────────────────┤
│  ┌─────────────────────────────────┐   │
│  │  OnlyOffice Container           │   │
│  │  - Port 80 (HTTP API)           │   │
│  │  - Conversion engine            │   │
│  │  - Reads from /storage          │   │
│  └─────────────────────────────────┘   │
│                                         │
│  ┌─────────────────────────────────┐   │
│  │  File-Server Container          │   │
│  │  - Port 9000 (HTTP API)         │   │
│  │  - Upload/Download files        │   │
│  │  - Same /storage volume         │   │
│  │  - Lightweight Python server    │   │
│  └─────────────────────────────────┘   │
│           ↓                             │
│  ┌─────────────────────────────────┐   │
│  │  Shared Volume                  │   │
│  │  /var/lib/onlyoffice-storage    │   │
│  │  (50Gi PersistentVolumeClaim)   │   │
│  └─────────────────────────────────┘   │
└─────────────────────────────────────────┘
```

### File-Server Endpoints

```powershell
# Health check
GET /health
Response: 200 OK

# Upload file (from console app)
POST /upload?filename=document.pdf
Content-Type: application/octet-stream
Body: <binary file content>

Response: 
{
  "status": "success",
  "filename": "document.pdf",
  "url": "http://127.0.0.1:9000/document.pdf"
}

# Download file (from OnlyOffice after conversion)
GET /document.docx
Response: 200 OK with file content
```

### File-Server Configuration (values.yaml)

```yaml
fileServer:
  enabled: true              # Enable/disable sidecar
  image:
    repository: python       # Python base image
    tag: "3.11-slim"        # Lightweight Python version
    pullPolicy: IfNotPresent
  port: 9000                # Container port
  storagePath: "/var/lib/onlyoffice-storage"  # Mount point
  persistence:
    size: "5Gi"             # Storage for sidecar
  resources:
    requests:
      memory: "128Mi"
      cpu: "50m"
    limits:
      memory: "256Mi"
      cpu: "200m"
```

### File-Server Port Forwarding

```powershell
# In one terminal, forward file-server port
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver-fileserver 9000:9000

# Verify file-server is accessible
curl http://localhost:9000/health

# Expected response: 200 OK
```

### Monitoring File-Server

```powershell
# View file-server logs
kubectl logs -n onlyoffice <pod-name> -c file-server -f

# Check file-server container status
kubectl get pods -n onlyoffice -o jsonpath='{.items[*].status.containerStatuses[?(@.name=="file-server")]}'

# Verify files are being stored
kubectl exec -n onlyoffice <pod-name> -c file-server -- ls -la /var/lib/onlyoffice-storage

# Check storage usage
kubectl exec -n onlyoffice <pod-name> -c file-server -- du -sh /var/lib/onlyoffice-storage
```

### Troubleshooting File-Server

```powershell
# File-server container not starting?
kubectl describe pod -n onlyoffice <pod-name> -c file-server

# Connection refused on port 9000?
# 1. Verify port-forward is active
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver-fileserver 9000:9000 -v 3

# 2. Check if service exists
kubectl get svc -n onlyoffice | grep fileserver

# 3. Verify service endpoints
kubectl get endpoints -n onlyoffice onlyoffice-onlyoffice-documentserver-fileserver

# 4. Test from inside pod
kubectl exec -n onlyoffice <pod-name> -- curl http://localhost:9000/health

# File upload failing?
# Check file-server logs
kubectl logs -n onlyoffice <pod-name> -c file-server --tail=50

# Storage full?
kubectl exec -n onlyoffice <pod-name> -c file-server -- df -h /var/lib/onlyoffice-storage
```

### File-Server Script

The file-server script is managed via ConfigMap and deployed at `/scripts/fileserver.py`. It provides:

```python
# Health check endpoint
@app.route('/health', methods=['GET'])

# File upload endpoint  
@app.route('/upload', methods=['POST'])

# File download endpoint
@app.route('/<filename>', methods=['GET'])

# List files endpoint (optional)
@app.route('/', methods=['GET'])
```

For details, see: `k8s/helm-chart/onlyoffice/templates/fileserver-configmap.yaml`

## Storage Configuration

### PersistentVolume Status

```powershell
# Check storage class
kubectl get storageclass

# List persistent volumes
kubectl get pv

# List persistent volume claims
kubectl get pvc -n onlyoffice

# Describe PVC
kubectl describe pvc -n onlyoffice onlyoffice-onlyoffice-documentserver-files-pvc
```

### Storage Verification

```powershell
# Access pod to verify storage mount
kubectl exec -it -n onlyoffice <pod-name> -c onlyoffice -- /bin/bash

# Inside pod:
df -h                                    # Show mounted filesystems
ls -la /var/lib/onlyoffice-storage       # Check storage directory
```

## Helm Chart Updates & Upgrades

### Update Helm Values

```powershell
# Upgrade deployment with new values
helm upgrade onlyoffice ./helm-chart/onlyoffice `
  -n onlyoffice `
  --set replicaCount=5 `
  --set persistence.size=20Gi

# Verify upgrade
helm status onlyoffice -n onlyoffice
kubectl get pods -n onlyoffice -w
```

### Rollback Deployment

```powershell
# List release history
helm history onlyoffice -n onlyoffice

# Rollback to previous release
helm rollback onlyoffice -n onlyoffice

# Rollback to specific revision
helm rollback onlyoffice 1 -n onlyoffice
```

### Uninstall Deployment

```powershell
# Remove Helm release
helm uninstall onlyoffice -n onlyoffice

# Verify removal
helm list -n onlyoffice
kubectl get pods -n onlyoffice

# Delete namespace (if desired)
kubectl delete namespace onlyoffice
```

## Health Checks & Monitoring

### OnlyOffice Health Check

```powershell
# Check OnlyOffice health endpoint
curl http://localhost:8080/health

# Expected response:
# {
#   "status": "healthy",
#   "version": "8.0.1"
# }
```

### File-Server Health Check

```powershell
# Check file-server health
curl http://localhost:9000/health

# Expected response: 200 OK
```

### Pod Health Status

```powershell
# Check pod readiness/liveness
kubectl get pods -n onlyoffice -o wide

# Detailed pod conditions
kubectl describe pod -n onlyoffice <pod-name>

# Look for:
# Conditions:
#   Type              Status
#   ----              ------
#   Initialized       True
#   Ready             True
#   ContainersReady   True
#   PodScheduled      True
```

### Resource Usage

```powershell
# View pod resource usage
kubectl top pods -n onlyoffice

# View node resource usage
kubectl top nodes
```

## Troubleshooting

### Pod Won't Start

```powershell
# Check pod events
kubectl describe pod -n onlyoffice <pod-name>

# Check logs
kubectl logs -n onlyoffice <pod-name>

# Common issues:
# - Insufficient resources: scale down replicas
# - Storage not ready: verify microk8s storage add-on enabled
# - Pull image error: check image name and registry
```

### Connection Issues

```powershell
# Test port forwarding
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver 8080:80

# Try connection (in another terminal)
curl http://localhost:8080

# If fails, check pod networking
kubectl exec -n onlyoffice <pod-name> -- curl http://localhost
```

### Storage Issues

```powershell
# Check PVC status
kubectl describe pvc -n onlyoffice

# Check available storage
microk8s status
df -h

# If out of space, increase storage allocation
```

### Database Connection Issues

```powershell
# Check PostgreSQL pod
kubectl get pods -n onlyoffice -l app=postgresql

# Check PostgreSQL logs
kubectl logs -n onlyoffice <postgresql-pod>

# Test connection from OnlyOffice pod
kubectl exec -n onlyoffice <onlyoffice-pod> -- \
  psql -h postgresql-service -U onlyoffice_user -d onlyoffice -c "SELECT 1"
```

## Docker Images

### Image Sources

```yaml
# OnlyOffice Document Server
Image: onlyoffice/documentserver:8.0.1
Registry: Docker Hub (onlyoffice organization)
Pulls: Automatically by Kubernetes

# PostgreSQL
Image: postgres:13
Registry: Docker Hub (library)

# RabbitMQ
Image: rabbitmq:3.9
Registry: Docker Hub (library)

# Python File Server
Image: python:3.11-slim
Registry: Docker Hub (library)
```

### Image Management

```powershell
# List downloaded images on microK8s
microk8s.ctr images ls

# Force image pull (useful for updates)
kubectl set image deployment/onlyoffice-documentserver \
  onlyoffice=onlyoffice/documentserver:8.0.1-update \
  -n onlyoffice --record

# Pull specific image tag
microk8s ctr image pull docker.io/onlyoffice/documentserver:8.0.1
```

## Common Commands Reference

### Deployment Operations

```powershell
# Install
helm install onlyoffice ./helm-chart/onlyoffice -n onlyoffice --create-namespace

# Upgrade
helm upgrade onlyoffice ./helm-chart/onlyoffice -n onlyoffice

# Rollback
helm rollback onlyoffice -n onlyoffice

# Uninstall
helm uninstall onlyoffice -n onlyoffice
```

### Monitoring

```powershell
# Watch pods
kubectl get pods -n onlyoffice -w

# View logs
kubectl logs -n onlyoffice <pod-name> -f

# Describe resource
kubectl describe pod -n onlyoffice <pod-name>
```

### Maintenance

```powershell
# Scale replicas
kubectl scale deployment onlyoffice-documentserver --replicas=3 -n onlyoffice

# Restart deployment
kubectl rollout restart deployment/onlyoffice-documentserver -n onlyoffice

# Delete pod (forces restart)
kubectl delete pod -n onlyoffice <pod-name>
```

### Testing

```powershell
# Port forward
kubectl port-forward svc/onlyoffice-onlyoffice-documentserver 8080:80 -n onlyoffice

# Test endpoint
curl http://localhost:8080

# Interactive shell in pod
kubectl exec -it -n onlyoffice <pod-name> -- /bin/bash
```

## Production Deployment Checklist

- [ ] Change default JWT secret
- [ ] Update database credentials
- [ ] Configure external PostgreSQL (recommended)
- [ ] Set appropriate resource limits
- [ ] Enable autoscaling (replicas 3-5)
- [ ] Configure ingress for HTTPS
- [ ] Set up monitoring and logging
- [ ] Configure backup strategy
- [ ] Test failover scenarios
- [ ] Document deployment configuration
- [ ] Create runbook for operations team

## Next Steps

1. ✅ Complete Kubernetes setup
2. → Build and deploy the console application
   See: `CONSOLE_APP_USAGE.md`
3. → Test end-to-end conversion
4. → Configure for production
   See: `ARCHITECTURE.md` for production considerations

All components are now ready for document conversion workloads!
