# Quick Start Guide - OnlyOffice with microK8s

This guide provides step-by-step instructions to deploy OnlyOffice Document Server on a local microK8s cluster.

## Prerequisites

### Windows Setup

1. **Install microK8s for Windows**

   ```powershell
   # Using Chocolatey
   choco install microk8s
   
   # Or download from: https://microk8s.io/docs/install-windows
   ```

2. **Install kubectl**

   ```powershell
   choco install kubernetes-cli
   ```

3. **Install Helm**

   ```powershell
   choco install kubernetes-helm
   ```

4. **Verify Installation**

   ```powershell
   microk8s version
   kubectl version --client
   helm version
   ```

## Step 1: Start microK8s

```powershell
# Start microK8s
microk8s start

# Wait for it to be ready
microk8s status --wait-ready
```

## Step 2: Enable Required Addons

```powershell
# Enable storage (required for PersistentVolumes)
microk8s enable storage

# Enable DNS
microk8s enable dns

# Optional: Enable ingress
microk8s enable ingress

# Verify addons
microk8s status
```

## Step 3: Configure kubectl

```powershell
# Setup kubeconfig
microk8s config | Set-Content ~/.kube/config
$env:KUBECONFIG = "$env:USERPROFILE\.kube\config"

# Test connection
kubectl cluster-info
```

## Step 4: Create Namespace

```powershell
# Create dedicated namespace
kubectl create namespace onlyoffice

# Set as default namespace
kubectl config set-context --current --namespace=onlyoffice
```

## Step 5: Deploy OnlyOffice with Helm

### Option A: Deploy with Default Settings

```powershell
# Navigate to helm chart directory
cd helm-chart

# Install the chart
helm install onlyoffice ./onlyoffice `
  --namespace onlyoffice `
  --values onlyoffice/values.yaml

# Check installation progress
kubectl get pods -n onlyoffice -w
```

### Option B: Deploy with Custom Values

Create a file `custom-values.yaml`:

```yaml
replicaCount: 1

podConfig:
  resources:
    requests:
      memory: "1Gi"
      cpu: "500m"
    limits:
      memory: "2Gi"
      cpu: "1000m"

persistence:
  size: 30Gi

autoscaling:
  enabled: false
```

Then install:

```powershell
helm install onlyoffice ./onlyoffice `
  --namespace onlyoffice `
  --values custom-values.yaml
```

## Step 6: Verify Deployment

```powershell
# Check all pods are running
kubectl get pods -n onlyoffice

# Check services
kubectl get svc -n onlyoffice

# Check persistent volumes
kubectl get pvc -n onlyoffice

# View deployment details
kubectl describe deployment -n onlyoffice
```

Wait until all pods show `Running` status and `1/1` ready.

## Step 7: Access the Service

### Method 1: Port Forward (Recommended for Testing)

```powershell
# Terminal 1: Setup port forwarding
kubectl port-forward svc/onlyoffice-documentserver 8080:80 -n onlyoffice

# Terminal 2: Test the service
curl http://localhost:8080/healthcheck
```

### Method 2: Get Node IP and Port

```powershell
# Get node IP
$NODE_IP = kubectl get nodes -o jsonpath='{.items[0].status.addresses[?(@.type=="InternalIP")].address}'

# Get service info
kubectl get svc onlyoffice-documentserver -n onlyoffice

# Access: http://<NODE_IP>:<PORT>
```

## Step 8: Build Console Application

```powershell
# Navigate to console app
cd OnlyOfficeConsoleApp

# Build the application
dotnet build -c Release

# Build creates: bin\Release\net8.0\OnlyOfficeConsoleApp.exe
```

## Step 9: Test Document Conversion

### Create Test Document

```powershell
# Create a simple test DOCX file (or use your own)
$testFile = "C:\Temp\test-document.docx"
```

### Run Conversion with Port Forward

```powershell
# Terminal 1: Keep port forward running
kubectl port-forward svc/onlyoffice-documentserver 8080:80 -n onlyoffice

# Terminal 2: Run converter
.\bin\Release\net8.0\OnlyOfficeConsoleApp.exe $testFile http://localhost:8080

# Output will be: C:\Temp\test-document.pdf
```

### Verify Output

```powershell
# Check if PDF was created
Get-Item C:\Temp\test-document.pdf
Get-Item C:\Temp\test-document.pdf | Select-Object Length

# View file details
Get-Item C:\Temp\test-document.pdf | fl
```

## Viewing Logs

```powershell
# View latest logs
kubectl logs deployment/onlyoffice-documentserver -n onlyoffice --tail=50

# Follow logs
kubectl logs -f deployment/onlyoffice-documentserver -n onlyoffice

# View specific pod logs
kubectl logs <pod-name> -n onlyoffice
```

## Checking Database and RabbitMQ

```powershell
# Check PostgreSQL status
kubectl get pods -n onlyoffice | grep postgresql

# Check RabbitMQ status
kubectl get pods -n onlyoffice | grep rabbitmq

# Port forward to RabbitMQ management UI
kubectl port-forward svc/rabbitmq 15672:15672 -n onlyoffice
# Access: http://localhost:15672 (user: guest, pass: guest)
```

## Scaling the Deployment

```powershell
# Manually scale
kubectl scale deployment onlyoffice-documentserver --replicas=3 -n onlyoffice

# Check scaling
kubectl get deployment onlyoffice-documentserver -n onlyoffice
kubectl get pods -n onlyoffice
```

## Useful Commands

### Monitor Resources

```powershell
# Check CPU and memory usage
kubectl top pods -n onlyoffice
kubectl top nodes
```

### Enter a Pod

```powershell
# Get pod name
$POD_NAME = kubectl get pods -n onlyoffice -o name | Select-Object -First 1

# Execute bash in pod
kubectl exec -it $POD_NAME -n onlyoffice -- bash
```

### Port Forwarding to Database

```powershell
# PostgreSQL
kubectl port-forward svc/postgresql 5432:5432 -n onlyoffice

# Then connect: psql -h localhost -U onlyoffice -d onlyoffice
```

## Cleanup

```powershell
# Delete Helm release
helm uninstall onlyoffice -n onlyoffice

# Delete namespace
kubectl delete namespace onlyoffice

# Stop microK8s
microk8s stop

# Full reset (WARNING: Deletes all data)
microk8s reset
```

## Troubleshooting

### Pods Not Starting

```powershell
# Check pod status
kubectl describe pod <pod-name> -n onlyoffice

# View events
kubectl get events -n onlyoffice --sort-by='.lastTimestamp'
```

### Check Storage

```powershell
# List storage classes
kubectl get storageclass

# Check PVC
kubectl get pvc -n onlyoffice
kubectl describe pvc -n onlyoffice
```

### View All Resources

```powershell
# Get all resources in namespace
kubectl get all -n onlyoffice

# Get detailed info
kubectl get all -n onlyoffice -o wide
```

### Check Helm Status

```powershell
# List installed releases
helm list -n onlyoffice

# Get release values
helm get values onlyoffice -n onlyoffice

# Get release manifest
helm get manifest onlyoffice -n onlyoffice
```

## Next Steps

1. **Configure Production Settings**: Update `values.yaml` with production values
2. **Setup SSL/TLS**: Configure Ingress with certificates
3. **Monitor Performance**: Implement metrics collection
4. **Backup Strategy**: Setup PostgreSQL backups
5. **Integrate API**: Use the .NET console app or build web API

## Additional Resources

- [microK8s Documentation](https://microk8s.io/docs)
- [Kubernetes Documentation](https://kubernetes.io/docs/)
- [Helm Documentation](https://helm.sh/docs/)
- [OnlyOffice GitHub](https://github.com/ONLYOFFICE/DocumentServer)
- [OnlyOffice API Documentation](https://api.onlyoffice.com/)

## Support

For issues:
1. Check logs: `kubectl logs -f deployment/onlyoffice-documentserver -n onlyoffice`
2. Verify pods: `kubectl get pods -n onlyoffice`
3. Test connectivity: `kubectl port-forward svc/onlyoffice-documentserver 8080:80 -n onlyoffice`
4. Check OnlyOffice GitHub issues
