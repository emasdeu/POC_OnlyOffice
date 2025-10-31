# Helm Chart Configuration Examples

This file contains various configuration examples for different deployment scenarios.

## Development Environment (microK8s)

**File: `values-dev.yaml`**

```yaml
# Minimal resources for development
replicaCount: 1

podConfig:
  resources:
    requests:
      memory: "512Mi"
      cpu: "250m"
    limits:
      memory: "1Gi"
      cpu: "500m"

persistence:
  size: 20Gi
  storageClassName: "microk8s-hostpath"

autoscaling:
  enabled: false

service:
  type: ClusterIP

ingress:
  enabled: false

# Reduce replicas for test services
env:
  JWT_ENABLED: "true"
  JWT_SECRET: "dev-secret-key-change-in-production"
```

**Install:**
```bash
helm install onlyoffice ./onlyoffice -f values-dev.yaml -n onlyoffice
```

## Production Environment

**File: `values-prod.yaml`**

```yaml
# Production settings
replicaCount: 3

podConfig:
  resources:
    requests:
      memory: "4Gi"
      cpu: "2000m"
    limits:
      memory: "8Gi"
      cpu: "4000m"

persistence:
  size: 100Gi
  storageClassName: "fast-ssd"  # Use appropriate storage class

autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 10
  targetCPUUtilizationPercentage: 70
  targetMemoryUtilizationPercentage: 75

service:
  type: LoadBalancer

ingress:
  enabled: true
  className: "nginx"
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
  hosts:
    - host: onlyoffice.example.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: onlyoffice-tls
      hosts:
        - onlyoffice.example.com

# External database
postgresql:
  enabled: false
  externalDatabase:
    host: "postgres.example.com"
    port: 5432
    username: "onlyoffice"
    password: "secure-prod-password"
    database: "onlyoffice"

# Security
env:
  JWT_ENABLED: "true"
  JWT_SECRET: "your-secure-random-key-here"

networkPolicy:
  enabled: true

podDisruptionBudget:
  enabled: true
  minAvailable: 2
```

**Install:**
```bash
helm install onlyoffice ./onlyoffice -f values-prod.yaml -n onlyoffice
```

## Testing with External Services

**File: `values-external-services.yaml`**

```yaml
# Use external PostgreSQL and RabbitMQ

replicaCount: 2

postgresql:
  enabled: false
  externalDatabase:
    host: "postgres.external-db.internal"
    port: 5432
    username: "onlyoffice_user"
    password: "external-db-password"
    database: "onlyoffice_db"

rabbitmq:
  enabled: false
  # Configure external RabbitMQ via environment variables
  # This requires template modifications

persistence:
  size: 50Gi
  storageClassName: "standard"
```

## High Availability Setup

**File: `values-ha.yaml`**

```yaml
replicaCount: 3

podConfig:
  resources:
    requests:
      memory: "3Gi"
      cpu: "1500m"
    limits:
      memory: "6Gi"
      cpu: "3000m"
  
  affinity:
    podAntiAffinity:
      preferredDuringSchedulingIgnoredDuringExecution:
      - weight: 100
        podAffinityTerm:
          labelSelector:
            matchExpressions:
            - key: app.kubernetes.io/name
              operator: In
              values:
              - onlyoffice-documentserver
          topologyKey: kubernetes.io/hostname

autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 10

podDisruptionBudget:
  enabled: true
  minAvailable: 2

networkPolicy:
  enabled: true
```

## Low Resource Environment

**File: `values-low-resources.yaml`**

```yaml
replicaCount: 1

podConfig:
  resources:
    requests:
      memory: "256Mi"
      cpu: "100m"
    limits:
      memory: "512Mi"
      cpu: "250m"

persistence:
  size: 10Gi
  storageClassName: "standard"

autoscaling:
  enabled: false

service:
  type: ClusterIP
```

## Quick Migration Command Examples

```bash
# Development to Staging
helm upgrade onlyoffice ./onlyoffice \
  -f values-staging.yaml \
  -n onlyoffice

# Staging to Production
helm upgrade onlyoffice ./onlyoffice \
  -f values-prod.yaml \
  -n onlyoffice

# Rollback to previous version
helm rollback onlyoffice -n onlyoffice

# View upgrade history
helm history onlyoffice -n onlyoffice
```
