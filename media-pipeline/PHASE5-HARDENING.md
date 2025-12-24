# Phase 5: Production Hardening & Best Practices

## Security

### 1. API Key Management

**DO**:
- Store API keys in Kubernetes Secrets (never in ConfigMaps)
- Use separate keys for each service
- Rotate keys regularly (every 90 days)
- Use read-only keys where possible (Jellyfin allows this)

**Implementation**:
```bash
# Create secret with actual API keys
kubectl create secret generic media-api-keys -n media \
  --from-literal=jellyfin-api-key="$(cat /secure/jellyfin-key.txt)" \
  --from-literal=radarr-api-key="$(cat /secure/radarr-key.txt)" \
  --from-literal=sonarr-api-key="$(cat /secure/sonarr-key.txt)"

# Seal the secret (if using Sealed Secrets)
kubeseal <secret.yaml >sealed-secret.yaml
kubectl apply -f sealed-secret.yaml
```

**Retrieval in Code**:
```python
# API keys loaded from environment (populated by Kubernetes)
JELLYFIN_API_KEY = os.getenv("JELLYFIN_API_KEY")
```

### 2. Network Policies

Restrict traffic to/from media services.

**File**: `clusters/microk8s/media/network-policy.yaml`
```yaml
---
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: media-services
  namespace: media
spec:
  podSelector: {}
  policyTypes:
  - Ingress
  - Egress
  ingress:
  # Allow ingress-nginx
  - from:
    - namespaceSelector:
        matchLabels:
          name: ingress-nginx
    ports:
    - protocol: TCP
      port: 8080  # qBittorrent
    - protocol: TCP
      port: 7878  # Radarr
    - protocol: TCP
      port: 8989  # Sonarr
    - protocol: TCP
      port: 8096  # Jellyfin
  
  # Allow cleanup job to access APIs
  - from:
    - podSelector:
        matchLabels:
          app: media-cleanup
  
  egress:
  # Allow DNS
  - to:
    - namespaceSelector:
        matchLabels:
          name: kube-system
    ports:
    - protocol: UDP
      port: 53
  
  # Allow internet (for downloads, metadata, etc.)
  - to:
    - ipBlock:
        cidr: 0.0.0.0/0
        except:
        - 169.254.169.254/32  # Block AWS metadata
```

### 3. RBAC for Cleanup Job

Cleanup job has minimal permissions (already implemented in `cleanup-cronjob.yaml`):
- Read ConfigMaps and Secrets in `media` namespace
- No write permissions
- No access to other namespaces

### 4. NFS Mount Security

**DO**:
- Use NFSv4 (not NFSv3) for better security
- Configure NFS export with `no_root_squash` only if necessary
- Prefer `root_squash` (maps root to nobody)
- Use firewall rules to restrict NFS access to cluster nodes only

**NAS Configuration** (Synology example):
```
Allowed clients: 192.168.2.0/24
Privilege: Read/Write
Squash: Map all users to admin (for write access)
Security: sys (or krb5 for Kerberos)
```

**Kubernetes Mount Options**:
```yaml
mountOptions:
  - nfsvers=4.1
  - hard  # Don't give up on mount failure
  - timeo=600  # 60 second timeout
  - retrans=2  # Retry twice
  - rsize=1048576  # 1MB read size
  - wsize=1048576  # 1MB write size
```

---

## Common Pitfalls & Mitigations

### 1. Atmos Audio Transcoding (CRITICAL)

**Problem**: Jellyfin transcodes TrueHD Atmos to AC3 5.1, destroying Atmos metadata.

**Root Causes**:
- Client doesn't support TrueHD passthrough
- Network bandwidth too low
- Subtitles enabled (forces video transcode → audio transcode)
- Transcoding settings incorrectly configured

**Prevention**:
1. **Jellyfin Settings**:
   - Dashboard > Playback > "Allow audio playback that requires conversion": **DISABLE**
   - Dashboard > Transcoding > Hardware acceleration: **None**

2. **Client Requirements**:
   - Use official Jellyfin apps (support passthrough)
   - Ensure client device connected via HDMI to AVR (not optical/ARC without eARC)
   - Verify AVR supports TrueHD/DTS-HD MA

3. **Network**:
   - Minimum 100 Mbps for 4K REMUX streaming
   - Use wired Ethernet (not Wi-Fi) for 4K clients
   - Check during playback: Dashboard > Activity → should show "Direct Play"

4. **Verification Script**:
```bash
# Check if transcoding occurred
kubectl logs -n media deployment/jellyfin | grep -i transcode

# If found, investigate:
# - Client capabilities
# - Bandwidth
# - Subtitle settings
```

**Detection**:
```python
# Add to cleanup script logging
def log_media_info(episode_or_movie):
    """Log codec info to detect potential transcoding issues"""
    media_info = episode_or_movie.get('mediaInfo', {})
    audio_codec = media_info.get('audioCodec', 'unknown')
    
    if audio_codec in ['TrueHD', 'DTS-HD MA', 'DTS:X']:
        logger.info(f"Lossless audio detected: {audio_codec}")
    elif audio_codec in ['AC3', 'EAC3']:
        logger.warning(f"Lossy audio detected: {audio_codec} - verify source quality")
```

### 2. NAS Unavailability

**Problem**: NFS mount fails, media services crash-loop.

**Symptoms**:
- Pods stuck in `ContainerCreating` or `CrashLoopBackOff`
- "Unable to attach or mount volumes" in pod events

**Mitigation**:
1. **PV Reclaim Policy**: `Retain` (never `Delete`)
   ```yaml
   persistentVolumeReclaimPolicy: Retain
   ```

2. **Mount Options**:
   ```yaml
   mountOptions:
     - hard  # Retry indefinitely (blocks pod startup)
     - soft  # Alternative: fail after timeout (allows pod to start with errors)
   ```

3. **Liveness/Readiness Probes**:
   ```yaml
   livenessProbe:
     initialDelaySeconds: 60  # Allow time for NFS mount
     periodSeconds: 30
     timeoutSeconds: 10
     failureThreshold: 3
   ```

4. **Monitoring**:
   ```bash
   # Check NFS mount status on nodes
   kubectl get events -n media --sort-by='.lastTimestamp'
   
   # Check NFS server reachability
   nc -zv 192.168.2.200 2049
   ```

5. **Recovery**:
   ```bash
   # If NFS server was down and came back up
   kubectl rollout restart -n media deployment/jellyfin
   kubectl rollout restart -n media deployment/radarr
   kubectl rollout restart -n media deployment/sonarr
   ```

### 3. Metadata Drift

**Problem**: Jellyfin and Radarr/Sonarr databases out of sync.

**Scenarios**:
- File renamed outside of *arr apps
- Manual file deletion
- Database corruption
- Filesystem vs. database mismatch

**Detection**:
```bash
# Check for orphaned database entries (no file)
# Radarr API
curl -H "X-Api-Key: $RADARR_KEY" \
  http://radarr.media.svc.cluster.local:7878/api/v3/movie \
  | jq '.[] | select(.hasFile == false) | .title'

# Sonarr API
curl -H "X-Api-Key: $SONARR_KEY" \
  http://sonarr.media.svc.cluster.local:8989/api/v3/episode \
  | jq '.[] | select(.hasFile == false and .monitored == true) | .title'
```

**Prevention**:
1. **NEVER delete files manually** - always use *arr APIs
2. Regular database backups:
   ```bash
   # Backup Radarr database
   kubectl exec -n media deployment/radarr -- \
     cp /config/radarr.db /config/backups/radarr-$(date +%Y%m%d).db
   ```

3. **Rescan libraries** periodically:
   ```bash
   # Force Jellyfin library scan
   curl -X POST \
     "http://jellyfin.media.svc.cluster.local:8096/Library/Refresh" \
     -H "X-Emby-Token: $JELLYFIN_KEY"
   ```

**Recovery**:
```bash
# Radarr: Refresh monitored movies
curl -X POST \
  http://radarr.media.svc.cluster.local:7878/api/v3/command \
  -H "X-Api-Key: $RADARR_KEY" \
  -H "Content-Type: application/json" \
  -d '{"name": "RefreshMonitoredDownloads"}'

# Sonarr: Refresh series
curl -X POST \
  http://sonarr.media.svc.cluster.local:8989/api/v3/command \
  -H "X-Api-Key: $SONARR_KEY" \
  -d '{"name": "RefreshSeries"}'
```

### 4. Cleanup Job Failures

**Problem**: Cleanup script fails mid-execution.

**Causes**:
- API temporarily unavailable
- Network timeout
- Database lock
- Invalid API response

**Resilience Strategies**:

1. **Retry Logic**:
```python
from tenacity import retry, stop_after_attempt, wait_exponential

@retry(stop=stop_after_attempt(3), wait=wait_exponential(min=1, max=10))
def delete_with_retry(mapper, item_id, dry_run):
    return mapper.delete_radarr_movie(item_id, dry_run=dry_run)
```

2. **Checkpoint State**:
```python
# Save progress to file/ConfigMap
checkpoint_file = "/var/lib/cleanup/checkpoint.json"

def save_checkpoint(deleted_items):
    with open(checkpoint_file, 'w') as f:
        json.dump(deleted_items, f)

def load_checkpoint():
    if os.path.exists(checkpoint_file):
        with open(checkpoint_file, 'r') as f:
            return set(json.load(f))
    return set()

# In cleanup loop
already_deleted = load_checkpoint()

for movie_id in movies_to_delete:
    if movie_id in already_deleted:
        continue  # Skip already processed
    
    if delete_movie(movie_id):
        already_deleted.add(movie_id)
        save_checkpoint(list(already_deleted))
```

3. **Kubernetes Job Settings**:
```yaml
spec:
  backoffLimit: 2  # Retry failed jobs twice
  activeDeadlineSeconds: 3600  # Timeout after 1 hour
```

4. **Monitoring**:
```bash
# Check cleanup job history
kubectl get jobs -n media -l app=media-cleanup

# View failed job logs
kubectl logs -n media job/media-cleanup-xxxxx
```

### 5. Race Conditions

**Problem**: Cleanup runs while user is actively downloading/watching.

**Scenario**:
1. User adds movie in Radarr
2. Movie downloads
3. User watches movie
4. Cleanup job runs before grace period expires
5. Movie deleted while user watching = **BAD**

**Solution**: Grace period prevents this (5 days default).

**Additional Protection**:
```python
def is_currently_playing(jellyfin_url, api_key, item_id):
    """Check if item is currently being played"""
    response = requests.get(
        f"{jellyfin_url}/Sessions",
        headers={"X-Emby-Token": api_key}
    )
    sessions = response.json()
    
    for session in sessions:
        if session.get('NowPlayingItem', {}).get('Id') == item_id:
            return True
    return False

# In cleanup logic
if is_currently_playing(jellyfin_url, api_key, movie_id):
    logger.info(f"Skipping {movie_name}: Currently playing")
    continue
```

---

## Logging Strategy

### 1. Structured Logging

Use JSON logging for easy parsing:
```python
import json
import logging

class JSONFormatter(logging.Formatter):
    def format(self, record):
        log_obj = {
            "timestamp": self.formatTime(record),
            "level": record.levelname,
            "logger": record.name,
            "message": record.getMessage(),
            "module": record.module,
            "function": record.funcName
        }
        
        # Add extra fields
        if hasattr(record, 'movie_id'):
            log_obj['movie_id'] = record.movie_id
        if hasattr(record, 'action'):
            log_obj['action'] = record.action
        
        return json.dumps(log_obj)

# Configure logger
handler = logging.StreamHandler()
handler.setFormatter(JSONFormatter())
logger.addHandler(handler)

# Usage
logger.info("Deleting movie", extra={"movie_id": 123, "action": "delete"})
```

### 2. Log Levels

- **DEBUG**: API requests/responses, path matching details
- **INFO**: Deletion decisions, skipped items, summary stats
- **WARNING**: Items not found, API errors (retryable)
- **ERROR**: Unrecoverable failures, invalid configuration
- **CRITICAL**: System-level failures (NAS unavailable, auth failure)

### 3. Log Aggregation (Loki Integration)

Already implemented in your cluster:
```yaml
# Loki will auto-collect logs from all media namespace pods
# Query examples:
{namespace="media", app="media-cleanup"}
{namespace="media"} |= "deleted"
{namespace="media"} |= "ERROR"
```

**Grafana Dashboards**:
- Cleanup job success rate
- Items deleted per run
- API error rates
- Grace period distribution

---

## Monitoring & Alerting

### 1. Prometheus Metrics (Optional Enhancement)

Expose metrics from cleanup script:
```python
from prometheus_client import Counter, Gauge, start_http_server

# Metrics
movies_deleted = Counter('media_cleanup_movies_deleted_total', 'Total movies deleted')
episodes_deleted = Counter('media_cleanup_episodes_deleted_total', 'Total episodes deleted')
cleanup_duration = Gauge('media_cleanup_duration_seconds', 'Cleanup job duration')

# Expose metrics endpoint
start_http_server(8000)

# In cleanup code
movies_deleted.inc()
```

### 2. Alert Rules

```yaml
# PrometheusRule
apiVersion: monitoring.coreos.com/v1
kind: PrometheusRule
metadata:
  name: media-cleanup-alerts
  namespace: media
spec:
  groups:
  - name: media-cleanup
    rules:
    - alert: CleanupJobFailed
      expr: kube_job_status_failed{namespace="media",job_name=~"media-cleanup.*"} > 0
      for: 5m
      labels:
        severity: warning
      annotations:
        summary: "Media cleanup job failed"
        description: "Cleanup job {{ $labels.job_name }} has failed"
    
    - alert: MediaServicesDown
      expr: up{namespace="media"} == 0
      for: 5m
      labels:
        severity: critical
      annotations:
        summary: "Media service {{ $labels.job }} is down"
```

---

## Backup Strategy

### 1. Configuration Backups

```bash
# Backup all *arr configurations
kubectl exec -n media deployment/radarr -- tar czf /tmp/radarr-config.tar.gz /config
kubectl cp media/radarr-xxx:/tmp/radarr-config.tar.gz ./backups/radarr-$(date +%Y%m%d).tar.gz

# Same for Sonarr, Jellyfin
```

### 2. Database Backups

```bash
# Automated backup CronJob
apiVersion: batch/v1
kind: CronJob
metadata:
  name: media-config-backup
  namespace: media
spec:
  schedule: "0 2 * * 0"  # Weekly on Sunday at 2 AM
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: backup
            image: alpine:latest
            command:
            - /bin/sh
            - -c
            - |
              apk add --no-cache tar gzip
              tar czf /backup/media-config-$(date +%Y%m%d).tar.gz /config
            volumeMounts:
            - name: config
              mountPath: /config
            - name: backup
              mountPath: /backup
          volumes:
          - name: config
            persistentVolumeClaim:
              claimName: media-config
          - name: backup
            nfs:
              server: 192.168.2.200
              path: /volume1/backups/media
```

---

## Performance Optimization

### 1. API Call Batching

```python
# Instead of N API calls to check each movie:
# 1 API call to get all movies, then in-memory filtering

# BAD
for jellyfin_movie in watched_movies:
    radarr_movie = api_call_get_movie_by_tmdb(tmdb_id)

# GOOD
all_radarr_movies = api_call_get_all_movies()  # 1 call
for jellyfin_movie in watched_movies:
    radarr_movie = find_in_cache(all_radarr_movies, tmdb_id)
```

### 2. Concurrent Processing

```python
from concurrent.futures import ThreadPoolExecutor, as_completed

def delete_movie_safe(mapper, movie_id, dry_run):
    try:
        return mapper.delete_radarr_movie(movie_id, dry_run=dry_run)
    except Exception as e:
        logger.error(f"Failed to delete movie {movie_id}: {e}")
        return False

# Parallel deletion (use with caution - may overwhelm APIs)
with ThreadPoolExecutor(max_workers=5) as executor:
    futures = {
        executor.submit(delete_movie_safe, mapper, mid, dry_run): mid
        for mid in movies_to_delete
    }
    
    for future in as_completed(futures):
        movie_id = futures[future]
        try:
            success = future.result()
            if success:
                stats.movies_deleted += 1
        except Exception as e:
            logger.error(f"Exception deleting movie {movie_id}: {e}")
            stats.movies_failed += 1
```

---

## Deployment Checklist

- [ ] API keys stored in Sealed Secrets
- [ ] Network policies applied
- [ ] NFS server accessible and configured correctly
- [ ] Cleanup job running in dry-run mode initially
- [ ] Logs aggregated to Loki
- [ ] Monitoring dashboards created
- [ ] Backup strategy implemented
- [ ] Jellyfin transcoding disabled
- [ ] Grace period configured appropriately
- [ ] "keep" tags created in Radarr/Sonarr
- [ ] Test cleanup with sample data
- [ ] Verify Atmos audio preserved in downloads
- [ ] Document runbooks for common issues
