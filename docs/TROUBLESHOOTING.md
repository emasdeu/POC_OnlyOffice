# Troubleshooting & Lessons Learned

A comprehensive guide documenting the challenges encountered during OnlyOffice Document Server deployment on Kubernetes and solutions that worked.

## Table of Contents

1. [Common Issues & Solutions](#common-issues--solutions)
2. [Port Forwarding Troubleshooting](#port-forwarding-troubleshooting)
3. [Kubernetes Pod Issues](#kubernetes-pod-issues)
4. [Conversion API Problems](#conversion-api-problems)
5. [Storage & File Access Issues](#storage--file-access-issues)
6. [Debugging Techniques](#debugging-techniques)
7. [Lessons Learned](#lessons-learned)

---

## Common Issues & Solutions

### Issue 1: Connection Refused - Port Forwarding Not Working

**Symptom**:
```
curl: (7) Failed to connect to localhost port 8080: Connection refused
```

**Root Causes**:
1. Port forwarding not started
2. Pod not yet running
3. Port already in use by another process
4. Service not created

**Solution**:

```powershell
# Step 1: Verify port forwarding is active
netstat -ano | Select-String ":8080"
# If nothing shown, port forwarding is not running

# Step 2: Start port forwarding in background
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver 8080:80

# Step 3: Verify pod is running
kubectl get pods -n onlyoffice
# Look for "Running" status, not "Pending" or "CrashLoopBackOff"

# Step 4: Check if port is in use by another process
netstat -ano | Select-String ":8080" | Select-Object Line
# If found, kill the process:
taskkill /PID <PID> /F

# Step 5: Verify service exists
kubectl get svc -n onlyoffice
# Should show: onlyoffice-onlyoffice-documentserver
```

**Prevention**:
- Use terminal tabs: one for port-forward, one for testing
- Add port-forward to startup scripts
- Use background flag: `kubectl port-forward ... &`

---

### Issue 2: Pod Status Stuck in "Pending"

**Symptom**:
```
NAME                                    READY   STATUS    RESTARTS
onlyoffice-onlyoffice-documentserver    0/2     Pending   0
```

**Root Causes**:
1. Storage not available
2. Insufficient cluster resources
3. Image not found or cannot be pulled
4. PVC not bound to PV

**Solution**:

```powershell
# Step 1: Describe the pod to see events
kubectl describe pod -n onlyoffice <pod-name>
# Look at "Events" section - shows why pod is pending

# Step 2: Check storage is enabled
microk8s status
# Look for: storage: enabled
# If not:
microk8s enable storage

# Step 3: Check if PVC is bound
kubectl get pvc -n onlyoffice
# Should show "Bound" status, not "Pending"

# Step 4: Describe PVC to see issues
kubectl describe pvc -n onlyoffice <pvc-name>

# Step 5: Check available resources
kubectl describe node
# Look for "Allocatable" resources (CPU, Memory)

# Step 6: Check image availability
microk8s.ctr images ls | grep onlyoffice
# If not found, image pull may have failed
```

**Example Output**:
```
Events:
  Type     Reason            Age
  ----     ------            ---
  Warning  FailedScheduling  2m
  Message: persistentvolumeclaim "onlyoffice-files-pvc" not found
```

**Prevention**:
- Always enable storage before deploying: `microk8s enable storage`
- Use `kubectl get pvc` before creating deployments
- Monitor events: `kubectl get events -n onlyoffice`

---

### Issue 3: Pod Crashes with "CrashLoopBackOff"

**Symptom**:
```
NAME                                    READY   STATUS              RESTARTS
onlyoffice-onlyoffice-documentserver    0/2     CrashLoopBackOff    5
```

**Root Causes**:
1. Container exited with error
2. Liveness probe failed
3. Configuration error (missing env vars, secrets)
4. Resource limits exceeded

**Solution**:

```powershell
# Step 1: View container logs
kubectl logs -n onlyoffice <pod-name>
# Shows application output up to crash

# Step 2: Check logs from previous container run
kubectl logs -n onlyoffice <pod-name> --previous
# Shows logs from before the crash

# Step 3: Describe pod for probe failures
kubectl describe pod -n onlyoffice <pod-name>
# Look for "Liveness probe failed" or "Readiness probe failed"

# Step 4: Check specific container logs
kubectl logs -n onlyoffice <pod-name> -c onlyoffice
kubectl logs -n onlyoffice <pod-name> -c file-server

# Step 5: Check resource usage
kubectl top pods -n onlyoffice
# Compare with limits in deployment
```

**Common Error Messages & Solutions**:

| Error | Cause | Solution |
|-------|-------|----------|
| `ECONNREFUSED: connection refused` | PostgreSQL/RabbitMQ not ready | Wait for dependencies |
| `Liveness probe failed: HTTP probe failed` | Application not responding | Check application logs |
| `OOMKilled` | Out of memory | Increase memory limits |
| `Permission denied` | Volume mount issue | Check volume permissions |

**Prevention**:
- Use `--wait` flag with Helm to ensure dependencies start first
- Implement gradual startup with init containers
- Set appropriate resource requests/limits

---

## Port Forwarding Troubleshooting

### Multiple Port Forwards in One Terminal

**Problem**: Need to forward both OnlyOffice (8080) and File-Server (9000) simultaneously

**Bad Approach** (blocks terminal):
```powershell
# This blocks terminal - can't run other commands
kubectl port-forward svc/onlyoffice-onlyoffice-documentserver 8080:80 -n onlyoffice

# Terminal frozen - can't do anything else
```

**Good Approach** (background):
```powershell
# Start first port forward in background
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver 8080:80 &

# Start second port forward
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver-fileserver 9000:9000

# In another terminal tab, can test both services
```

**Best Approach** (multiple terminals):
```powershell
# Terminal 1: Forward OnlyOffice
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver 8080:80

# Terminal 2 (separate): Forward File-Server
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver-fileserver 9000:9000

# Terminal 3 (separate): Run tests/app
.\OnlyOfficeConsoleApp.exe test.docx http://localhost:8080
```

### Port Already in Use

**Symptom**:
```
error: listen tcp 127.0.0.1:8080: bind: An attempt was made to use a socket address that is not valid.
```

**Solution**:
```powershell
# Find what's using port 8080
netstat -ano | Select-String ":8080"
# Example output: TCP    127.0.0.1:8080         LISTENING    12345

# Kill the process
taskkill /PID 12345 /F

# Verify port is free
netstat -ano | Select-String ":8080"
# Should return nothing

# Retry port forwarding
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver 8080:80
```

### Port Forward Terminates Unexpectedly

**Symptom**:
```
$ kubectl port-forward...
Forwarding from 127.0.0.1:8080 -> 80
Forwarding from [::1]:8080 -> 80
E1031 18:30:00.000000   12345 portforward.go:372] error creating error stream for port 8080->80: EOF
```

**Causes**:
1. Pod restarted while port-forward was active
2. Network connection dropped
3. Cluster connection lost

**Solution**:
```powershell
# Restart port forwarding
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver 8080:80 &

# Verify pod is still running
kubectl get pods -n onlyoffice

# Test connectivity again
curl http://localhost:8080
```

---

## Kubernetes Pod Issues

### Viewing Pod Logs Effectively

**Problem**: Logs are too verbose or truncated

**Solutions**:

```powershell
# View last 100 lines
kubectl logs -n onlyoffice <pod-name> --tail=100

# Follow logs in real-time (tail -f)
kubectl logs -n onlyoffice <pod-name> -f

# View logs from specific time
kubectl logs -n onlyoffice <pod-name> --since=5m
kubectl logs -n onlyoffice <pod-name> --since-time=2025-10-31T18:00:00Z

# View logs with timestamps
kubectl logs -n onlyoffice <pod-name> --timestamps=true

# Filter logs
kubectl logs -n onlyoffice <pod-name> | Select-String "ERROR"

# Save logs to file
kubectl logs -n onlyoffice <pod-name> > pod-logs.txt

# View all container logs
kubectl logs -n onlyoffice <pod-name> --all-containers=true

# View previous container run
kubectl logs -n onlyoffice <pod-name> --previous
```

### Accessing Pod Interactively

**Problem**: Need to debug inside running pod

**Solution**:

```powershell
# Open bash shell in pod
kubectl exec -it -n onlyoffice <pod-name> -- /bin/bash

# Inside the pod, useful commands:
ls -la /var/lib/onlyoffice-storage    # Check storage files
df -h                                  # Disk usage
ps aux                                 # Running processes
netstat -tlnp                          # Listening ports
curl http://localhost                  # Test internal connectivity

# For multi-container pods, specify container:
kubectl exec -it -n onlyoffice <pod-name> -c onlyoffice -- /bin/bash

# Run single command without shell
kubectl exec -n onlyoffice <pod-name> -- ls -la /var/lib/onlyoffice-storage
```

### Monitoring Pod Resource Usage

**Problem**: Pod using unexpected amounts of CPU/Memory

**Solution**:

```powershell
# View current resource usage
kubectl top pods -n onlyoffice

# View node resource usage
kubectl top nodes

# Describe pod to see limits
kubectl describe pod -n onlyoffice <pod-name>
# Look for "Limits:" and "Requests:"

# View pod resource metrics
kubectl get pods -n onlyoffice -o custom-columns=NAME:.metadata.name,CPU:..usage.cpu,MEMORY:..usage.memory
```

---

## Conversion API Problems

### Issue: Error Code -7 (Input Error) - RESOLVED ✅

**Symptom**:
```
✗ Conversion failed: OnlyOffice error code -7 (Input error)
Response: {"error":-7}
```

**Root Cause** (RESOLVED in current version):
The JWT token payload was missing required request parameters. OnlyOffice API requires:
- `filetype` (input format)
- `key` (unique conversion ID)
- `outputtype` (output format)
- `title` (document title)
- `url` (file location)

**What We Tried First** (FAILED):
```csharp
// WRONG - Only included timestamp
var payload = new {
    iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
};
```

**Solution** (NOW IMPLEMENTED ✅):
```csharp
// CORRECT - Include all required fields
var payload = new {
    filetype = request.FileType,
    key = request.Key,
    outputtype = request.OutputType,
    title = request.Title,
    url = request.Url
};
```

**Key Lesson**:
Always check the official API documentation for JWT payload requirements. Don't assume based on common JWT patterns.

**Reference**:
- https://api.onlyoffice.com/docs/docs-api/additional-api/conversion-api/request/

---

### Issue: Wrong API Endpoint - RESOLVED ✅

**Symptom**:
```
404 Not Found: Endpoint not found
or
405 Method Not Allowed
```

**What We Tried First** (FAILED):
- `/convert` (doesn't exist)
- `/ConvertService.ashx` (exists but different format)

**Correct Endpoint** (NOW IMPLEMENTED ✅):
```
POST /converter
```

**How We Found It**:
- Checked official OnlyOffice documentation
- Reviewed API examples
- Tested endpoints one by one

**Key Lesson**:
Always verify endpoints in official documentation before spending time debugging. The official docs are authoritative.

---

### Issue: Missing Accept Header - RESOLVED ✅

**Symptom**:
```
Response is XML instead of JSON
Parsing error: Cannot parse response
```

**What We Tried First** (FAILED):
```csharp
// WRONG - No Accept header
var request = new HttpRequestMessage(HttpMethod.Post, url);
request.Content = new StringContent(json, Encoding.UTF8, "application/json");
```

**Solution** (NOW IMPLEMENTED ✅):
```csharp
// CORRECT - Add Accept header
var request = new HttpRequestMessage(HttpMethod.Post, url);
request.Content = new StringContent(json, Encoding.UTF8, "application/json");
request.Headers.Add("Accept", "application/json");
```

**Key Lesson**:
HTTP headers matter. Always specify the desired response format with Accept header.

---

## Storage & File Access Issues

### Issue 1: EmptyDir Volume Doesn't Share Across Pod Replicas

**Problem**:
File uploaded to Pod A not accessible from Pod B because each pod had its own emptyDir volume.

**What We Used First** (FAILED):
```yaml
volumes:
- name: storage
  emptyDir: {}   # WRONG - ephemeral per pod, not shared
```

**Solution**:
```yaml
volumes:
- name: storage
  persistentVolumeClaim:
    claimName: onlyoffice-files-pvc  # CORRECT - shared across pods
```

**Created New File**:
```yaml
# helm-chart/onlyoffice/templates/files-pvc.yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: onlyoffice-onlyoffice-documentserver-files-pvc
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 5Gi
  storageClassName: microk8s-hostpath
```

**Key Lesson**:
For multi-replica deployments, use PersistentVolumeClaim (PVC), not emptyDir. emptyDir is only for temporary, pod-local data.

---

### Issue 2: Storage Not Available

**Symptom**:
```
PVC stuck in "Pending" state
kubectl get pvc shows "Pending"
```

**Root Cause**:
microK8s storage add-on not enabled.

**Solution**:
```powershell
# Enable storage addon
microk8s enable storage

# Verify it's enabled
microk8s status
# Should show: storage: enabled

# Check storage class
kubectl get storageclass
# Should show: microk8s-hostpath (default)

# Retry PVC creation
kubectl get pvc -n onlyoffice
# Should now show "Bound" status
```

---

### Issue 3: File Upload Fails

**Symptom**:
```
✗ Failed to upload file to storage server
Connection refused or timeout
```

**Root Causes**:
1. File-server pod not running
2. Port-forward not started for 9000
3. File-server service not created
4. File size too large

**Solution**:

```powershell
# Step 1: Check file-server pod
kubectl get pods -n onlyoffice | Select-String "file-server"

# Step 2: Check file-server service
kubectl get svc -n onlyoffice | Select-String "fileserver"

# Step 3: Port-forward file-server
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver-fileserver 9000:9000

# Step 4: Test file-server health
curl http://localhost:9000/health

# Step 5: Test file upload
$filePath = "C:\test.docx"
$uri = "http://localhost:9000/upload?filename=test.docx"
curl -X POST -F "file=@$filePath" $uri

# Step 6: Check file was stored
kubectl exec -n onlyoffice <pod-name> -- ls -la /var/lib/onlyoffice-storage
```

---

## Debugging Techniques

### Real-Time Log Monitoring

```powershell
# Watch logs while conversion runs
kubectl logs -n onlyoffice <pod-name> -c onlyoffice -f

# In another terminal, run conversion
.\OnlyOfficeConsoleApp.exe test.docx

# Watch logs for errors
```

### Pod Event Monitoring

```powershell
# Watch all events in namespace
kubectl get events -n onlyoffice -w

# Useful for seeing pod lifecycle:
# - SuccessfulCreate
# - FailedScheduling
# - Started
# - Rebooted
# - Created
```

### Network Connectivity Testing

```powershell
# From local machine
curl -v http://localhost:8080        # Test OnlyOffice
curl -v http://localhost:9000/health # Test File-Server

# From within pod
kubectl exec -n onlyoffice <pod-name> -- curl -v http://localhost
kubectl exec -n onlyoffice <pod-name> -- curl postgresql-service:5432

# DNS resolution within pod
kubectl exec -n onlyoffice <pod-name> -- nslookup onlyoffice-onlyoffice-documentserver
```

### Deployment Configuration Verification

```powershell
# View actual deployment spec
kubectl get deployment -n onlyoffice -o yaml

# View current pod spec
kubectl get pod -n onlyoffice <pod-name> -o yaml

# Compare with Helm values
helm get values onlyoffice -n onlyoffice

# Check what changed
helm diff upgrade onlyoffice ./helm-chart/onlyoffice -n onlyoffice
```

### Storage Inspection

```powershell
# From pod, check storage
kubectl exec -n onlyoffice <pod-name> -- sh -c "ls -lah /var/lib/onlyoffice-storage"

# Check inode usage
kubectl exec -n onlyoffice <pod-name> -- df -i

# Check if files are accessible
kubectl exec -n onlyoffice <pod-name> -- cat /var/lib/onlyoffice-storage/test.docx | wc -c
```

---

## Lessons Learned

### 1. **Always Check Official Documentation First**

**What We Learned**:
- JWT payload format varies by use case (not always just timestamp)
- API endpoints are documented in official sources
- Error codes have specific meanings

**How to Apply**:
- Before debugging, read official API docs
- Cross-reference your assumptions
- Check release notes for API changes

**Reference**:
- OnlyOffice API: https://api.onlyoffice.com/docs/

---

### 2. **Use Separate Terminal Tabs for Port Forwarding**

**What We Learned**:
- Foreground port-forward blocks the terminal
- Multiple services need multiple port-forwards
- Background processes can be lost

**Best Practice**:
```powershell
# Terminal 1: OnlyOffice
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver 8080:80

# Terminal 2: File-Server
kubectl port-forward -n onlyoffice svc/onlyoffice-onlyoffice-documentserver-fileserver 9000:9000

# Terminal 3: Run application & tests
.\OnlyOfficeConsoleApp.exe test.docx http://localhost:8080
```

---

### 3. **Storage Architecture Matters in Multi-Replica Deployments**

**What We Learned**:
- emptyDir volumes are ephemeral per pod
- Shared storage requires PersistentVolumeClaim
- Multi-replica deployments need shared storage from day one

**Key Decision**:
- Development: emptyDir acceptable for single pod
- Testing+: Always use PVC for shared storage

---

### 4. **HTTP Headers Are Critical**

**What We Learned**:
- Accept header controls response format
- Content-Type header matters
- Missing headers can cause silent failures

**Always Include**:
```csharp
request.Headers.Accept.Add(
    new MediaTypeWithQualityHeaderValue("application/json")
);
request.Content = new StringContent(json, Encoding.UTF8, "application/json");
```

---

### 5. **Enable Kubernetes Add-ons Before Deploying**

**What We Learned**:
- Storage must be enabled: `microk8s enable storage`
- DNS must be enabled: `microk8s enable dns`
- Missing add-ons cause silent pod failures

**Checklist**:
```powershell
# Before any deployment:
microk8s enable storage
microk8s enable dns

# Verify
microk8s status
```

---

### 6. **Log Files Are Your Best Debug Tool**

**What We Learned**:
- Pod logs show what's happening
- Previous container logs show crash reasons
- Event logs show scheduling issues

**Debug Process**:
1. Check pod events: `kubectl describe pod`
2. View current logs: `kubectl logs`
3. View previous logs: `kubectl logs --previous`
4. Watch logs while testing: `kubectl logs -f`

---

### 7. **Test Individual Components First**

**What We Learned**:
- Test file upload before conversion
- Test endpoint availability before integration
- Test storage access from within pod

**Isolation Strategy**:
```powershell
# Test each component separately:
curl http://localhost:8080               # OnlyOffice
curl http://localhost:9000/health        # File-Server
curl -X POST -F file=@test.docx \
  http://localhost:9000/upload            # Upload
```

---

### 8. **JWT Token Generation Must Match API Expectations**

**What We Learned**:
- Not all tokens are created equal
- Payload structure is API-specific
- Token signing must use correct algorithm and secret

**Verification Process**:
1. Check API documentation for required fields
2. Decode token (jwt.io) to verify payload
3. Verify signature algorithm matches API expectations
4. Check secret key matches server config

---

### 9. **Pod Readiness != Service Readiness**

**What We Learned**:
- Pod "Running" doesn't mean service is ready
- Liveness probe passes but app might not be ready
- Need to wait for service to be available

**Solution**:
```powershell
# Wait for pods
kubectl wait --for=condition=ready pod \
  -l app=onlyoffice \
  -n onlyoffice \
  --timeout=300s

# Test endpoint (retry with delays)
$retries = 0
while ($retries -lt 10) {
    try {
        curl http://localhost:8080
        break
    }
    catch {
        $retries++
        Start-Sleep -Seconds 5
    }
}
```

---

### 10. **Knowledge of Tools is Essential**

**Critical Tools & Commands**:

| Tool | Critical Commands |
|------|-------------------|
| kubectl | logs, describe, exec, port-forward, get |
| curl | Testing endpoints, header inspection |
| netstat | Port availability checking |
| docker/ctr | Image inspection |
| Helm | Installation, values override, rollback |

**Time Investment**:
Learning these tools well saves hours of debugging.

---

## Bidirectional Document Conversion

### PDF to DOCX Conversion - FULLY SUPPORTED ✅

The OnlyOffice Conversion API supports PDF to DOCX conversion using the exact same endpoint and mechanism as DOCX to PDF!

**What We Discovered**:
- Same API endpoint: `/converter`
- Same JWT payload structure
- Only difference: `filetype: "pdf"` and `outputtype: "docx"`
- Conversion time: ~1.5 seconds
- Works seamlessly through the console app

**How It Works**:
```powershell
# Console app auto-detects PDF and converts to DOCX
.\OnlyOfficeConsoleApp.exe document.pdf http://localhost:8080

# Output: document.docx (converted and ready to edit)
```

**Technical Details**:
```json
// PDF to DOCX Request
{
  "filetype": "pdf",
  "key": "unique-id",
  "outputtype": "docx",
  "title": "document.pdf",
  "url": "http://file-server/document.pdf",
  "token": "jwt-token"
}
```

**Test Results**:
- Input: test_pdf_source.pdf (27,511 bytes)
- Output: test_pdf_source.docx (8,831 bytes)
- Success: ✅ YES
- Time: 1,483ms

**Important Notes**:
- PDF → DOCX is a lossy conversion (some formatting may be lost)
- Output is fully editable DOCX format
- Works with any PDF file
- Same automatic format detection applies

**Extending to Other Formats**:
The same API supports many more conversions:
- XLSX → PDF (Excel)
- PPTX → PDF (PowerPoint)
- ODP → PDF (OpenDocument)
- PDF → XLSX (PDF to Excel)
- And many more...

See `docs/PDF_TO_DOCX_CONVERSION.md` for complete conversion guide and supported format matrix.

---

## Quick Reference: Debugging Workflow

When something doesn't work:

```powershell
# 1. Check pod status
kubectl get pods -n onlyoffice

# 2. If not running, describe for events
kubectl describe pod -n onlyoffice <pod-name>

# 3. Check logs
kubectl logs -n onlyoffice <pod-name>

# 4. Check previous logs if crashed
kubectl logs -n onlyoffice <pod-name> --previous

# 5. Test connectivity
kubectl port-forward svc/... 8080:80 -n onlyoffice
curl http://localhost:8080

# 6. Shell into pod for detailed inspection
kubectl exec -it -n onlyoffice <pod-name> -- /bin/bash

# 7. Inside pod, test internal connectivity
curl http://localhost
ls -la /var/lib/onlyoffice-storage
netstat -tlnp
```

---

## Summary

This guide captures the real issues we encountered and their solutions. The key takeaways:

✅ **Always check official documentation first**
✅ **Use separate terminals for port-forwarding**
✅ **Use PersistentVolumeClaim for shared storage**
✅ **Enable Kubernetes add-ons before deploying**
✅ **Logs are your best debugging tool**
✅ **Test components in isolation**
✅ **Verify HTTP headers are correct**
✅ **Understand that configuration matters**

These lessons will help you avoid the same pitfalls and debug issues more efficiently!
