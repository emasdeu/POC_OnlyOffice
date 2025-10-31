# OnlyOffice Document Server Helm Chart

A comprehensive Helm chart for deploying OnlyOffice Document Server on Kubernetes, optimized for microK8s deployments.

## Chart Information

- **Chart Name**: onlyoffice-documentserver
- **Chart Version**: 1.0.0
- **App Version**: 8.0
- **Kubernetes Version**: 1.20+

## Prerequisites

### Required Software

- **microK8s** 1.20+ (with storage addon enabled)
- **kubectl** command-line tool
- **Helm** 3.0+

### System Requirements

- **Minimum Resources**: 4GB RAM, 2 CPU cores
- **Recommended**: 8GB RAM, 4 CPU cores
- **Storage**: 50GB minimum for documents and cache

### microK8s Addons

Enable required addons:

```bash
microk8s enable storage
microk8s enable dns
microk8s enable registry
```

Verify addons:

```bash
microk8s status
```

## Installation

### 1. Add the Helm Chart Repository

```bash
# If using a remote repository (future)
helm repo add onlyoffice-poc https://your-repository.com/charts
helm repo update
```

### 2. Install the Chart

#### Option A: Using Default Values (Recommended for Development)

```bash
helm install onlyoffice ./onlyoffice \
  --namespace default \
  --create-namespace
```

#### Option B: Using Custom Values

```bash
helm install onlyoffice ./onlyoffice \
  -f custom-values.yaml \
  --namespace onlyoffice \
  --create-namespace
```

#### Option C: Install with Specific Parameters

```bash
helm install onlyoffice ./onlyoffice \
  --namespace onlyoffice \
  --create-namespace \
  --set replicaCount=3 \
  --set persistence.size=100Gi \
  --set service.type=LoadBalancer
```

### 3. Verify Installation

Check the deployment status:

```bash
kubectl get pods -n onlyoffice
kubectl get svc -n onlyoffice
kubectl get pvc -n onlyoffice
```

View deployment details:

```bash
kubectl describe deployment -n onlyoffice
kubectl logs -f deployment/onlyoffice-documentserver -n onlyoffice
```

## Configuration

### Common Values

#### Replicas

```yaml
replicaCount: 2
```

#### Resource Limits

```yaml
podConfig:
  resources:
    requests:
      memory: "2Gi"
      cpu: "1000m"
    limits:
      memory: "4Gi"
      cpu: "2000m"
```

#### Storage Class (for microK8s)

```yaml
persistence:
  storageClassName: "microk8s-hostpath"
  size: 50Gi
```

#### Service Type

For microK8s without LoadBalancer, use:

```yaml
service:
  type: ClusterIP
```

For Kubernetes with LoadBalancer support:

```yaml
service:
  type: LoadBalancer
```

#### JWT Configuration

```yaml
env:
  JWT_ENABLED: "true"
  JWT_SECRET: "your-secure-secret-key"
```

### Advanced Configuration

#### Using External PostgreSQL

```yaml
postgresql:
  enabled: false
  externalDatabase:
    host: "postgres.example.com"
    port: 5432
    username: "onlyoffice"
    password: "secure-password"
    database: "onlyoffice"
```

#### Using External RabbitMQ

Modify the Helm chart templates to support external RabbitMQ by updating environment variables.

#### Autoscaling

```yaml
autoscaling:
  enabled: true
  minReplicas: 2
  maxReplicas: 5
  targetCPUUtilizationPercentage: 70
  targetMemoryUtilizationPercentage: 80
```

#### Ingress

```yaml
ingress:
  enabled: true
  className: "nginx"
  hosts:
    - host: onlyoffice.local
      paths:
        - path: /
          pathType: Prefix
```

## Accessing the Service

### Within the Cluster

```bash
# Service DNS name
http://onlyoffice-documentserver.default.svc.cluster.local
```

### Port Forwarding (Development)

```bash
kubectl port-forward svc/onlyoffice-documentserver 8080:80 -n onlyoffice
```

Then access: `http://localhost:8080`

### Via NodePort

```bash
kubectl patch svc onlyoffice-documentserver -n onlyoffice -p '{"spec": {"type": "NodePort"}}'
```

Get the NodePort:

```bash
kubectl get svc onlyoffice-documentserver -n onlyoffice
```

### Via Ingress

Enable ingress in values and ensure ingress controller is installed:

```bash
# For microK8s with nginx ingress
microk8s enable ingress
```

## Usage with Console Application

### Local Testing (Port Forwarding)

```bash
# Terminal 1: Setup port forward
kubectl port-forward svc/onlyoffice-documentserver 8080:80 -n onlyoffice

# Terminal 2: Run converter
.\OnlyOfficeConsoleApp.exe C:\documents\document.docx http://localhost:8080
```

### From External Client

```bash
# Get the service endpoint
kubectl get svc onlyoffice-documentserver -n onlyoffice

# Use the external IP or NodePort
.\OnlyOfficeConsoleApp.exe C:\documents\document.docx http://<EXTERNAL-IP>:80
```

## Monitoring

### View Logs

```bash
# Current logs
kubectl logs deployment/onlyoffice-documentserver -n onlyoffice

# Tail logs
kubectl logs -f deployment/onlyoffice-documentserver -n onlyoffice

# Previous pod logs (if crashed)
kubectl logs deployment/onlyoffice-documentserver -n onlyoffice --previous
```

### Check Pod Status

```bash
kubectl describe pod <pod-name> -n onlyoffice
```

### Database Connection

```bash
# Connect to PostgreSQL pod
kubectl exec -it postgresql-<id> -n onlyoffice -- psql -U onlyoffice -d onlyoffice
```

### RabbitMQ Management

```bash
# Port forward to RabbitMQ management UI (15672)
kubectl port-forward svc/rabbitmq 15672:15672 -n onlyoffice
```

Access: `http://localhost:15672` (default: guest/guest)

## Troubleshooting

### Pods Not Starting

**Check pod logs:**
```bash
kubectl logs deployment/onlyoffice-documentserver -n onlyoffice
```

**Check events:**
```bash
kubectl describe pod <pod-name> -n onlyoffice
```

**Common issues:**
- Insufficient resources: Add more nodes or reduce resource requests
- Storage not available: Verify microK8s storage addon is enabled
- Database not ready: Wait for PostgreSQL pod to be ready

### Storage Issues

**Check PVC status:**
```bash
kubectl get pvc -n onlyoffice
kubectl describe pvc onlyoffice-documentserver-pvc -n onlyoffice
```

**Verify storage class:**
```bash
kubectl get storageclass
```

### Connection Issues

**Test connectivity:**
```bash
kubectl run -it --rm debug --image=busybox --restart=Never -- sh
nc -zv onlyoffice-documentserver 80
```

### Database Connection Errors

**Verify database credentials:**
```bash
kubectl get secret onlyoffice-documentserver-db -n onlyoffice -o yaml
```

**Test database connection:**
```bash
kubectl exec -it <onlyoffice-pod> -n onlyoffice -- bash
nc -zv postgresql 5432
```

### Performance Issues

**Check resource usage:**
```bash
kubectl top pods -n onlyoffice
```

**Review HPA status:**
```bash
kubectl get hpa -n onlyoffice
kubectl describe hpa onlyoffice-documentserver -n onlyoffice
```

**Increase resource limits in values.yaml and upgrade:**
```bash
helm upgrade onlyoffice ./onlyoffice -f values.yaml -n onlyoffice
```

## Upgrading

### Update Chart Values

```bash
helm upgrade onlyoffice ./onlyoffice \
  --namespace onlyoffice \
  -f values.yaml
```

### Upgrade to New Version

```bash
# Update chart repository
helm repo update

# Upgrade
helm upgrade onlyoffice onlyoffice/onlyoffice-documentserver \
  --namespace onlyoffice
```

## Uninstalling

### Remove the Release

```bash
helm uninstall onlyoffice -n onlyoffice
```

### Remove Namespace

```bash
kubectl delete namespace onlyoffice
```

### Clean Up Persistent Volumes

```bash
kubectl delete pvc --all -n onlyoffice
```

## Security Considerations

1. **JWT Secret**: Change the default JWT secret in production
2. **Database Password**: Use strong, unique passwords
3. **Resource Limits**: Set appropriate limits to prevent DoS
4. **Network Policies**: Enable network policies for segmentation
5. **RBAC**: Restrict service account permissions
6. **TLS/SSL**: Configure SSL certificates for production
7. **Image Registry**: Use private image registry for production

### Production Checklist

- [ ] Update JWT_SECRET to a strong random value
- [ ] Configure external PostgreSQL with backups
- [ ] Setup persistent storage with snapshots
- [ ] Enable HTTPS with valid certificates
- [ ] Configure Ingress controller with TLS
- [ ] Setup monitoring and alerting
- [ ] Configure resource limits and requests
- [ ] Enable pod security policies
- [ ] Configure network policies
- [ ] Setup pod disruption budgets
- [ ] Enable horizontal pod autoscaling
- [ ] Configure log aggregation

## Performance Tuning

### For microK8s

```yaml
# Reduce replicas for development
replicaCount: 1

# Adjust resource requests
podConfig:
  resources:
    requests:
      memory: "1Gi"
      cpu: "500m"
    limits:
      memory: "2Gi"
      cpu: "1000m"

# Disable autoscaling
autoscaling:
  enabled: false
```

### For Production

```yaml
# Increase replicas
replicaCount: 3

# Increase resource limits
podConfig:
  resources:
    requests:
      memory: "4Gi"
      cpu: "2000m"
    limits:
      memory: "8Gi"
      cpu: "4000m"

# Enable autoscaling
autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 10
  targetCPUUtilizationPercentage: 70
```

## Support

For issues and questions:
- OnlyOffice: https://github.com/ONLYOFFICE/DocumentServer
- Kubernetes: https://kubernetes.io/docs/
- microK8s: https://microk8s.io/docs

## License

This Helm chart is provided as-is. OnlyOffice Document Server is released under AGPL v3 license for self-hosted deployments.
