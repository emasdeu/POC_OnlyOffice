# Kubernetes & Docker Setup Guide

Complete instructions for deploying OnlyOffice Document Server to Kubernetes using Helm.

## Prerequisites

### Required Software

Local microK8s environment must be installed:

📚 **[Entorno local en K8s MicroK8s](https://confluence.wolterskluwer.io/spaces/TAASDO/pages/696502704/Entorno+local+en+K8s+Microk8s)**

### Configure kubectl Context

```powershell
# microK8s should be default context, verify:
kubectl config current-context
# Should show: microk8s. If not, set it:
kubectl config use-context microk8s
```

## Namespace Setup

### Create OnlyOffice Namespace

```powershell
# Create dedicated namespace
kubectl create namespace onlyoffice

# Set as default namespace (optional)
kubectl config set-context --current --namespace=onlyoffice
```


### Postgres Database Deployment

```powershell
# Deploy PostgreSQL to onlyoffice namespace
kubectl apply -f k8s/helm-chart/postgres-deployment.yaml

# Verify deployment
kubectl get pods -n onlyoffice -l app=postgres

# Expected output:
# NAME                        READY   STATUS    RESTARTS   AGE
# postgres-xxxxxxxxxx-xxxxx   1/1     Running   0          30s

## Helm Chart Deployment
```

### Install Helm Chart

```powershell
# Basic installation with default values
helm install onlyoffice ./k8s/helm-chart/onlyoffice -n onlyoffice

# Install with custom values
helm install onlyoffice ./helm-chart/onlyoffice `
  -n onlyoffice `
  --set replicaCount=2 `
  --set persistence.size=10Gi

```

### Verify Helm Installation

```powershell
# Check release status
helm list -n onlyoffice
helm status onlyoffice -n onlyoffice

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
# onlyoffice-postgresql-0                         1/1     Running   0
```

### Pod Details

```powershell
# Describe a pod
kubectl describe pod -n onlyoffice <pod-name>

# View pod logs
kubectl logs -n onlyoffice <pod-name>

# View specific container logs
kubectl logs -n onlyoffice <pod-name> -c onlyoffice-documentserver

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

