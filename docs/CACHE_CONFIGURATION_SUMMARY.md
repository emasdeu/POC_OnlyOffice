# Cache Expiration Configuration Summary

## Changes Made

### 1. **Updated Helm Chart Values** 
   - **File:** `k8s/helm-chart/onlyoffice/values.yaml`
   - **Change:** Enabled ConfigMap and set cache expiration to 30 minutes

   ```yaml
   configMap:
     enabled: true
     onlyofficeConfig:
       storage:
         urlExpires: 1800              # 30 minutes
       expire:
         files: 1800                   # 30 minutes  
         filesCron: "*/30 * * * * *"   # Cleanup every 30 minutes
   ```

### 2. **Updated ConfigMap Template**
   - **File:** `k8s/helm-chart/onlyoffice/templates/configmap.yaml`
   - **Change:** Created dynamic ConfigMap that injects cache settings into OnlyOffice

   ```json
   {
     "storage": {
       "fs": {"urlExpires": 1800},
       "urlExpires": 1800
     },
     "services": {
       "CoAuthoring": {
         "expire": {
           "files": 1800,
           "filesCron": "*/30 * * * * *"
         }
       }
     }
   }
   ```

### 3. **Created Documentation**
   - **File:** `docs/CACHE_CONFIGURATION.md`
   - **Content:** Complete guide to cache settings, monitoring, and troubleshooting

## What Changed

| Setting | Before | After | Impact |
|---------|--------|-------|--------|
| **URL Expiration** | 7 days (604800s) | 30 minutes (1800s) | Download URLs expire faster |
| **Cache Cleanup** | 24 hours (86400s) | 30 minutes (1800s) | Files deleted sooner |
| **Cleanup Frequency** | Every hour | Every 30 minutes | More frequent cleanup |

## Benefits

✅ **Faster storage reclamation** - Cached files removed after 30 minutes instead of 24 hours  
✅ **Reduced PVC usage** - Cache won't accumulate unnecessarily  
✅ **Suitable for batch conversions** - 30 minutes is enough to download converted files  
✅ **Configurable** - Can adjust in values.yaml for different use cases

## How to Apply Changes

### Option 1: Deploy to Existing Cluster

```powershell
# Update Helm deployment with new configuration
helm upgrade onlyoffice k8s/helm-chart/onlyoffice -n onlyoffice

# Restart pods to pick up configuration
kubectl rollout restart deployment/onlyoffice-onlyoffice-documentserver -n onlyoffice

# Verify changes (wait 2-3 minutes for pod restart)
kubectl -n onlyoffice get pods
```

### Option 2: Fresh Deployment

```powershell
# Deploy fresh with updated configuration
helm install onlyoffice k8s/helm-chart/onlyoffice -n onlyoffice
```

## Verification

### Check ConfigMap Created

```powershell
kubectl get configmap -n onlyoffice onlyoffice-onlyoffice-documentserver-config
```

### View ConfigMap Content

```powershell
kubectl get configmap -n onlyoffice onlyoffice-onlyoffice-documentserver-config -o yaml
```

### Monitor Cache Cleanup

After 30 minutes of first conversion:

```powershell
# Check cache size (should be smaller)
kubectl -n onlyoffice exec deployment/onlyoffice-onlyoffice-documentserver -c onlyoffice-documentserver -- du -sh /var/lib/onlyoffice/documentserver/App_Data/cache

# List cached files (should have fewer entries)
kubectl -n onlyoffice exec deployment/onlyoffice-onlyoffice-documentserver -c onlyoffice-documentserver -- find /var/lib/onlyoffice/documentserver/App_Data/cache -type f
```

## Rollback (If Needed)

To revert to default 7-day expiration:

```yaml
configMap:
  enabled: false  # Or set back to original values
```

Then:
```powershell
helm upgrade onlyoffice k8s/helm-chart/onlyoffice -n onlyoffice
kubectl rollout restart deployment/onlyoffice-onlyoffice-documentserver -n onlyoffice
```

## Related Documentation

- **Full Configuration Guide:** `docs/CACHE_CONFIGURATION.md`
- **Architecture Overview:** `docs/ARCHITECTURE.md`
- **Kubernetes Setup:** `docs/KUBERNETES_SETUP.md`
- **Troubleshooting:** `docs/TROUBLESHOOTING.md`

## Next Steps

1. ✅ Apply Helm upgrade to cluster
2. ✅ Monitor cache behavior for 1 hour
3. ✅ Run healthcheck test: `.\run-conversion-test.ps1`
4. ✅ Verify cache files are cleaned up after 30 minutes
5. ✅ Adjust if needed based on actual usage patterns
