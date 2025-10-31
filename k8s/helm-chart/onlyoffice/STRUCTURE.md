# Helm Chart File Structure

```
helm-chart/onlyoffice/
├── Chart.yaml                      # Helm chart metadata
├── values.yaml                     # Default configuration values
├── README.md                        # Chart documentation
├── templates/
│   ├── _helpers.tpl               # Template helpers and functions
│   ├── deployment.yaml            # OnlyOffice Deployment resource
│   ├── service.yaml               # Kubernetes Service
│   ├── pvc.yaml                   # PersistentVolumeClaim for data
│   ├── serviceaccount.yaml        # ServiceAccount for pod identity
│   ├── secret.yaml                # Secrets for credentials
│   ├── configmap.yaml             # ConfigMap for configuration
│   ├── hpa.yaml                   # HorizontalPodAutoscaler
│   ├── pdb.yaml                   # PodDisruptionBudget
│   ├── ingress.yaml               # Ingress configuration
│   └── networkpolicy.yaml         # NetworkPolicy for security
```

## File Descriptions

### Chart.yaml
- Metadata about the chart (name, version, description)
- Chart dependencies
- Maintainer information

### values.yaml
- Default configuration for all chart settings
- Can be overridden during installation
- Organized by component

### Templates

#### _helpers.tpl
- Template functions for common labels
- Selectors and naming conventions
- Database and RabbitMQ host resolution

#### deployment.yaml
- Main OnlyOffice Document Server deployment
- Container configuration
- Environment variables
- Liveness and readiness probes
- Volume mounts

#### service.yaml
- Exposes OnlyOffice pods to the network
- ClusterIP (internal) or LoadBalancer (external) type
- Port mapping

#### pvc.yaml
- Persistent storage for document data
- Uses microK8s storage class
- 50GB default size

#### serviceaccount.yaml
- Pod identity and RBAC configuration
- Permissions for pod operations

#### secret.yaml
- Database credentials
- RabbitMQ credentials
- Base64 encoded values

#### configmap.yaml
- Configuration files
- Non-sensitive configuration data
- Local.json for OnlyOffice settings

#### hpa.yaml
- Automatic scaling based on CPU/memory
- Scales from 2 to 5 replicas
- Configurable thresholds

#### pdb.yaml
- Maintains availability during disruptions
- Minimum of 1 pod always available
- Prevents simultaneous eviction

#### ingress.yaml
- External HTTP/HTTPS access
- Optional - disabled by default
- Configurable hostname and TLS

#### networkpolicy.yaml
- Network traffic segmentation
- Ingress/Egress rules
- Restricts communication to necessary services
