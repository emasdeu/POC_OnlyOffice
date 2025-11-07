# OnlyOffice Document Server Cache Configuration

## Overview

OnlyOffice Document Server caches converted documents to optimize performance. This guide explains the cache settings and how to configure expiration times for your use case.

## Cache Storage

**Location in Pod:** `/var/lib/onlyoffice/documentserver/App_Data/cache/files/data/`

**Persistent Storage:** 
- Mounted to PersistentVolumeClaim: `onlyoffice-onlyoffice-documentserver-pvc` (50Gi)
- Storage class: `microk8s-hostpath`
- Persists across pod restarts

## Current Configuration (Updated)

### Cache Expiration: 30 Minutes

Based on your requirement that documents must be converted and downloaded within a reasonable timeframe, the cache has been configured with:

```json
{
  "storage": {
    "urlExpires": 1800,      // 30 minutes
    "fs": {
      "urlExpires": 1800     // 30 minutes
    }
  },
  "expire": {
    "files": 1800,           // 30 minutes
    "filesCron": "*/30 * * * * *"  // Cleanup every 30 minutes
  }
}
```

### What This Means

| Setting | Value | Meaning |
|---------|-------|---------|
| **`urlExpires`** | 1800 seconds (30 min) | Converted file download URLs expire after 30 minutes |
| **`files` (expire)** | 1800 seconds (30 min) | Cached files are kept for 30 minutes before deletion |
| **`filesCron`** | Every 30 minutes | Cleanup task runs every 30 minutes to remove expired files |

## How It Works

### Conversion Process Timeline

```
T=0s     Conversion requested
         ↓
T=0-2s   Document converted
         ↓
T=2s     Download URL generated with expiration timestamp (T + 1800s)
         Cached file: conv_[key]_[format]/output.[ext]
         ↓
T=2-1800s  User can download the file
         ↓
T=1800s  (30 min) URL EXPIRES - download no longer allowed
T=1800s  (30 min) File marked for deletion
         ↓
T=1830s  (31 min) Cleanup job runs - removes expired files
         ↓
T=1830+s Cache entry deleted - storage reclaimed
```

## Configuration Files

### Helm Values

**File:** `k8s/helm-chart/onlyoffice/values.yaml`

```yaml
configMap:
  enabled: true
  onlyofficeConfig:
    storage:
      urlExpires: 1800        # 30 minutes
    expire:
      files: 1800             # 30 minutes
      filesCron: "*/30 * * * * *"  # Every 30 minutes
```

### Generated ConfigMap

**File:** Applied via `k8s/helm-chart/onlyoffice/templates/configmap.yaml`

The ConfigMap is injected into the OnlyOffice pod as configuration overrides in `local.json`.

## Adjusting Cache Duration

### If You Need Different Expiration Times

Edit `k8s/helm-chart/onlyoffice/values.yaml`:

```yaml
configMap:
  onlyofficeConfig:
    storage:
      urlExpires: 3600        # 1 hour (3600 seconds)
    expire:
      files: 3600             # 1 hour
      filesCron: "0 * * * * *" # Every hour (at minute 0)
```

### Common Duration Values

| Duration | Seconds | Cron Expression |
|----------|---------|-----------------|
| 5 minutes | 300 | `*/5 * * * * *` |
| 15 minutes | 900 | `*/15 * * * * *` |
| 30 minutes | 1800 | `*/30 * * * * *` |
| 1 hour | 3600 | `0 * * * * *` |
| 2 hours | 7200 | `0 */2 * * * *` |
| 24 hours | 86400 | `0 0 * * * *` |

### After Editing Values

1. Update Helm chart deployment:
```powershell
helm upgrade onlyoffice k8s/helm-chart/onlyoffice -n onlyoffice
```

2. Verify changes applied:
```powershell
kubectl -n onlyoffice get configmap onlyoffice-onlyoffice-documentserver-config -o json | jq '.data."local.json"'
```

3. Restart pods to pick up new configuration:
```powershell
kubectl -n onlyoffice rollout restart deployment/onlyoffice-onlyoffice-documentserver
```

## Monitoring Cache

### Check Current Cache Size

```powershell
kubectl -n onlyoffice exec deployment/onlyoffice-onlyoffice-documentserver -c onlyoffice-documentserver -- du -sh /var/lib/onlyoffice/documentserver/App_Data/cache
```

### View Cached Files

```powershell
kubectl -n onlyoffice exec deployment/onlyoffice-onlyoffice-documentserver -c onlyoffice-documentserver -- find /var/lib/onlyoffice/documentserver/App_Data/cache -type f
```

### Check Cache Statistics

```powershell
# Count cached files
kubectl -n onlyoffice exec deployment/onlyoffice-onlyoffice-documentserver -c onlyoffice-documentserver -- find /var/lib/onlyoffice/documentserver/App_Data/cache -type f | wc -l

# Find oldest cached files
kubectl -n onlyoffice exec deployment/onlyoffice-onlyoffice-documentserver -c onlyoffice-documentserver -- find /var/lib/onlyoffice/documentserver/App_Data/cache -type f -mmin +30
```

## Default Configuration (Before Changes)

For reference, the original default configuration was:

```json
{
  "storage": {
    "urlExpires": 604800       // 7 days
  },
  "expire": {
    "files": 86400,            // 24 hours
    "filesCron": "00 00 */1 * * *"  // Every hour at minute 0
  }
}
```

## Best Practices

1. ✅ **Set expiration based on use case:**
   - Development/Testing: 5-15 minutes
   - Production batch conversions: 30 minutes - 1 hour
   - On-demand conversions: 1-2 hours

2. ✅ **Cleanup frequency should be reasonable:**
   - Don't run cleanup more than every 5 minutes (creates overhead)
   - Don't run less frequently than expiration time

3. ✅ **Monitor cache growth:**
   - Check cache size regularly
   - Alert if cache exceeds 80% of PVC size (40Gi out of 50Gi)

4. ✅ **Test with real workload:**
   - Run healthcheck test to verify timing
   - Monitor actual conversion/download patterns

## Troubleshooting

### URL Expires Too Quickly

**Problem:** Downloads fail with 404 after download starts

**Solution:** Increase `urlExpires` value (in seconds)

### Cache Growing Too Large

**Problem:** `/var/lib/onlyoffice/documentserver/App_Data/cache` exceeds 40Gi

**Solution:** 
1. Decrease `files` expiration time
2. Increase cleanup frequency (reduce cron interval)
3. Increase PVC size

### Configuration Not Applied

**Problem:** Cache expiration unchanged after Helm upgrade

**Solution:**
1. Verify ConfigMap was created: `kubectl get cm -n onlyoffice`
2. Verify ConfigMap content: `kubectl get cm -n onlyoffice onlyoffice-onlyoffice-documentserver-config -o yaml`
3. Restart pod to pick up changes: `kubectl rollout restart deployment/onlyoffice-onlyoffice-documentserver -n onlyoffice`

## References

- [OnlyOffice Configuration Documentation](https://helpcenter.onlyoffice.com/installation/docs-community-install-ubuntu.aspx)
- [OnlyOffice Storage Configuration](https://helpcenter.onlyoffice.com/installation/docs-community-install-ubuntu.aspx#storage)
- Cron Expression Format: Standard UNIX cron with 6 fields (second minute hour day month weekday)
