# Automated Media Pipeline - Implementation Complete

## Overview

This is a **fully implemented, production-ready** media automation pipeline running on Kubernetes that provides:

- **Automatic torrenting** via Radarr/Sonarr + qBittorrent
- **Netflix-like streaming** via Jellyfin with Direct Play (no transcoding)
- **Automatic cleanup** of watched content after configurable grace period
- **Dolby Atmos/TrueHD preservation** (no audio transcoding)
- **GitOps-ready** Kubernetes manifests

Everything added to Radarr/Sonarr is temporary by default. Content is automatically deleted 3-5 days after being watched, unless tagged with "keep".

---

## Architecture

```
┌─────────────┐
│   User      │
│  Requests   │
└──────┬──────┘
       │
       ▼
┌─────────────┐    ┌──────────────┐
│   Radarr    │───▶│  qBittorrent │
│  (Movies)   │    │   (VPN)      │
└──────┬──────┘    └──────┬───────┘
       │                  │
       ▼                  ▼
┌─────────────┐    ┌──────────────┐
│   Sonarr    │    │   Downloads  │
│    (TV)     │    │   (NFS)      │
└──────┬──────┘    └──────┬───────┘
       │                  │
       └──────┬───────────┘
              ▼
       ┌──────────────┐
       │    Media     │
       │ Content (NFS)│
       └──────┬───────┘
              │
              ▼
       ┌──────────────┐         ┌──────────────┐
       │   Jellyfin   │◀────────│    Users     │
       │  (Streaming) │         │  (Clients)   │
       └──────┬───────┘         └──────────────┘
              │
              ▼
       ┌──────────────┐
       │   Cleanup    │
       │   CronJob    │
       │ (Daily 3 AM) │
       └──────────────┘
```

---

## Implemented Phases

### ✅ PHASE 1: Core Pipeline
**Status**: Complete

**Deliverables**:
- Kubernetes manifests for all services
- NFS PersistentVolumes and PersistentVolumeClaims
- Quality profiles for 2160p REMUX with TrueHD Atmos
- Jellyfin Direct Play configuration
- Complete deployment documentation

**Files**:
- [clusters/microk8s/media/namespace.yaml](clusters/microk8s/media/namespace.yaml)
- [clusters/microk8s/media/storage.yaml](clusters/microk8s/media/storage.yaml)
- [clusters/microk8s/media/qbittorrent.yaml](clusters/microk8s/media/qbittorrent.yaml)
- [clusters/microk8s/media/radarr.yaml](clusters/microk8s/media/radarr.yaml)
- [clusters/microk8s/media/sonarr.yaml](clusters/microk8s/media/sonarr.yaml)
- [clusters/microk8s/media/jellyfin.yaml](clusters/microk8s/media/jellyfin.yaml)
- [clusters/microk8s/media/kustomization.yaml](clusters/microk8s/media/kustomization.yaml)
- [clusters/microk8s/media/README.md](clusters/microk8s/media/README.md) ← **Deployment Guide**

---

### ✅ PHASE 2: Watched State Access
**Status**: Complete

**Deliverables**:
- Complete API documentation for Jellyfin, Radarr, Sonarr
- Data mapping strategies (file path and metadata-based)
- Python library for API integration

**Files**:
- [media-pipeline/API-JELLYFIN.md](media-pipeline/API-JELLYFIN.md) ← **Jellyfin API Reference**
- [media-pipeline/API-RADARR-SONARR.md](media-pipeline/API-RADARR-SONARR.md) ← **Radarr/Sonarr API Reference**
- [media-pipeline/scripts/media_mapper.py](media-pipeline/scripts/media_mapper.py) ← **Mapping Library**

**Key Features**:
- Query watched movies/episodes
- Retrieve last played timestamps
- Map Jellyfin items to Radarr/Sonarr IDs
- Multi-user playback handling (primary user strategy)

---

### ✅ PHASE 3: Automated Cleanup
**Status**: Complete

**Deliverables**:
- Complete cleanup algorithm
- Production-ready Python script
- Kubernetes CronJob manifest
- Dry-run mode and idempotency guarantees

**Files**:
- [media-pipeline/scripts/cleanup.py](media-pipeline/scripts/cleanup.py) ← **Cleanup Script**
- [clusters/microk8s/media/cleanup-cronjob.yaml](clusters/microk8s/media/cleanup-cronjob.yaml) ← **CronJob Manifest**

**Key Features**:
- Configurable grace period (default: 5 days)
- Tag-based exclusions ("keep" tag)
- Dry-run mode (enabled by default)
- Comprehensive logging
- API-based deletion (preserves database integrity)

---

### ✅ PHASE 4: Series-Safe Deletion
**Status**: Complete

**Deliverables**:
- Season-aware deletion logic
- Edge case handling (ongoing series, partial seasons)
- Bulk deletion for complete seasons

**Files**:
- [media-pipeline/PHASE4-SERIES-LOGIC.md](media-pipeline/PHASE4-SERIES-LOGIC.md) ← **Implementation Guide**

**Key Features**:
- Prefer full-season deletion over per-episode
- Never delete partially watched seasons (configurable threshold)
- Protect latest season of ongoing series
- Handle specials (Season 0) individually
- Bulk API operations for efficiency

---

### ✅ PHASE 5: Production Hardening
**Status**: Complete

**Deliverables**:
- Security best practices
- Common pitfalls and mitigations
- Failure recovery procedures
- Monitoring and alerting strategies

**Files**:
- [media-pipeline/PHASE5-HARDENING.md](media-pipeline/PHASE5-HARDENING.md) ← **Production Guide**

**Key Topics**:
- API key management (Kubernetes Secrets)
- Network policies (restrict traffic)
- NFS security (NFSv4, mount options)
- Atmos transcoding prevention
- Metadata drift recovery
- Cleanup job resilience
- Logging aggregation (Loki)
- Performance optimization

---

## Quick Start

### 1. Prerequisites

- Kubernetes cluster (MicroK8s, k3s, etc.)
- NFS server for media storage
- VPN configured (for qBittorrent, handled externally)

### 2. Configure Storage

Update NFS settings in [clusters/microk8s/media/storage.yaml](clusters/microk8s/media/storage.yaml):

```yaml
nfs:
  server: 192.168.2.200      # Your NAS IP
  path: /volume1/media       # Media content path
```

### 3. Deploy Infrastructure

```bash
# Apply all manifests
kubectl apply -k clusters/microk8s/media/

# Verify deployment
kubectl get pods -n media
```

Expected output:
```
NAME                           READY   STATUS    RESTARTS   AGE
jellyfin-xxx                   1/1     Running   0          5m
qbittorrent-xxx                1/1     Running   0          5m
radarr-xxx                     1/1     Running   0          5m
sonarr-xxx                     1/1     Running   0          5m
```

### 4. Configure Applications

Follow the complete guide: [clusters/microk8s/media/README.md](clusters/microk8s/media/README.md)

**Summary**:
1. Configure qBittorrent (set download paths)
2. Configure Radarr (add qBittorrent client, quality profiles)
3. Configure Sonarr (add qBittorrent client, quality profiles)
4. Configure Jellyfin (add libraries, disable transcoding)

### 5. Store API Keys

```bash
# Retrieve API keys from each service
RADARR_KEY=$(kubectl exec -n media deployment/radarr -- cat /config/config.xml | grep -oPm1 "(?<=<ApiKey>)[^<]+")
SONARR_KEY=$(kubectl exec -n media deployment/sonarr -- cat /config/config.xml | grep -oPm1 "(?<=<ApiKey>)[^<]+")
JELLYFIN_KEY="YOUR_JELLYFIN_API_KEY"  # Create in UI: Dashboard > API Keys

# Create Kubernetes secret
kubectl create secret generic media-api-keys -n media \
  --from-literal=radarr-api-key="$RADARR_KEY" \
  --from-literal=sonarr-api-key="$SONARR_KEY" \
  --from-literal=jellyfin-api-key="$JELLYFIN_KEY"
```

### 6. Deploy Cleanup Scripts

```bash
# Create ConfigMap with cleanup scripts
kubectl create configmap media-cleanup-scripts -n media \
  --from-file=cleanup.py=media-pipeline/scripts/cleanup.py \
  --from-file=media_mapper.py=media-pipeline/scripts/media_mapper.py \
  --dry-run=client -o yaml | kubectl apply -f -

# Deploy CronJob (runs daily at 3 AM)
kubectl apply -f clusters/microk8s/media/cleanup-cronjob.yaml
```

### 7. Test Cleanup (Dry Run)

```bash
# Manually trigger cleanup job
kubectl create job -n media test-cleanup --from=cronjob/media-cleanup

# Watch logs
kubectl logs -n media -f job/test-cleanup

# Expected output:
# DRY RUN mode - no files will be deleted
# Found X watched movies
# Found Y watched episodes
# Eligible for deletion: ...
# [DRY RUN] Would delete ...
```

### 8. Enable Live Deletion

When ready to enable actual deletion:

```bash
# Edit ConfigMap
kubectl edit configmap media-cleanup-config -n media

# Change:
#   DRY_RUN: "true"
# To:
#   DRY_RUN: "false"

# Or via kubectl patch:
kubectl patch configmap media-cleanup-config -n media --type merge \
  -p '{"data":{"DRY_RUN":"false"}}'
```

---

## Configuration Reference

### Grace Period
How long to wait after content is watched before deleting:

```yaml
# In media-cleanup-config ConfigMap
GRACE_PERIOD_DAYS: "5"  # Default: 5 days
```

**Recommendations**:
- **3 days**: Aggressive, reclaim space quickly
- **5 days**: Balanced (default)
- **7 days**: Conservative, allows re-watching

### Keep Tags
Mark content to never be deleted:

1. Create "keep" tag in Radarr/Sonarr
2. Apply tag to movies/series you want to preserve
3. Cleanup script automatically skips tagged content

```yaml
# In media-cleanup-config ConfigMap
KEEP_TAG_LABEL: "keep"
```

### Primary User
Only track one user's watched state:

```yaml
# In media-cleanup-config ConfigMap
PRIMARY_USER_NAME: "admin"  # Jellyfin username
```

---

## Monitoring

### View Cleanup Logs

```bash
# Recent cleanup job logs
kubectl logs -n media -l app=media-cleanup --tail=100

# All cleanup jobs
kubectl get jobs -n media -l app=media-cleanup

# Specific job
kubectl logs -n media job/media-cleanup-12345
```

### Check Cleanup Stats

Cleanup script outputs summary:
```
CLEANUP SUMMARY
Movies:
  Watched: 45
  Eligible for deletion: 12
  Skipped (keep tag): 3
  Deleted: 9
  Failed: 0

Episodes:
  Watched: 234
  Eligible for deletion: 67
  Deleted: 67
  Failed: 0
```

### Loki Queries (if using existing Loki)

```
{namespace="media", app="media-cleanup"}
{namespace="media"} |= "deleted"
{namespace="media"} |= "ERROR"
```

---

## File Structure

```
home-automation-setup/
├── clusters/microk8s/media/
│   ├── namespace.yaml              # Namespace definition
│   ├── storage.yaml                # PV/PVC for NFS
│   ├── qbittorrent.yaml            # Download client
│   ├── radarr.yaml                 # Movie management
│   ├── sonarr.yaml                 # TV series management
│   ├── jellyfin.yaml               # Media server
│   ├── cleanup-cronjob.yaml        # Automated cleanup
│   ├── kustomization.yaml          # Kustomize config
│   └── README.md                   # Deployment guide
│
├── media-pipeline/
│   ├── API-JELLYFIN.md             # Jellyfin API reference
│   ├── API-RADARR-SONARR.md        # Radarr/Sonarr API reference
│   ├── PHASE4-SERIES-LOGIC.md      # Series deletion logic
│   ├── PHASE5-HARDENING.md         # Production hardening
│   └── scripts/
│       ├── media_mapper.py         # API mapping library
│       └── cleanup.py              # Cleanup script
```

---

## Common Operations

### Add Content
1. Go to Radarr/Sonarr web UI
2. Search and add movie/series
3. Content downloads automatically via qBittorrent
4. Appears in Jellyfin within minutes

### Watch Content
1. Open Jellyfin in browser or app
2. Select movie/episode
3. Verify "Direct Play" (no transcoding)
4. Check AVR displays "Dolby Atmos" or "DTS:X"

### Keep Content Permanently
1. In Radarr/Sonarr, edit movie/series
2. Add "keep" tag
3. Cleanup script will skip this content

### Manual Deletion
**NEVER delete files directly from filesystem!**

Always use Radarr/Sonarr APIs:
```bash
# Delete movie via Radarr
curl -X DELETE \
  "http://radarr.media.svc.cluster.local:7878/api/v3/movie/123?deleteFiles=true" \
  -H "X-Api-Key: $RADARR_KEY"

# Delete episode via Sonarr
curl -X DELETE \
  "http://sonarr.media.svc.cluster.local:8989/api/v3/episodefile/456" \
  -H "X-Api-Key: $SONARR_KEY"
```

---

## Troubleshooting

### Pods Not Starting

Check NFS connectivity:
```bash
# View events
kubectl get events -n media --sort-by='.lastTimestamp'

# Check NFS mount from node
nc -zv 192.168.2.200 2049
```

### Audio Transcoding Detected

Check Jellyfin playback:
```bash
# During playback, check activity
# Jellyfin UI → Dashboard → Activity
# Should show: "Direct Play" for video and audio

# If transcoding:
kubectl logs -n media deployment/jellyfin | grep -i transcode
```

**Fixes**:
- Disable "Allow audio playback that requires conversion" in Jellyfin
- Use wired Ethernet (not Wi-Fi) for 4K
- Verify client supports TrueHD/DTS-HD MA passthrough

### Cleanup Job Failed

View logs:
```bash
kubectl logs -n media -l app=media-cleanup --tail=200
```

Common causes:
- API keys invalid or expired
- Service temporarily unavailable
- NFS mount issue

### Content Not Deleting

Check dry-run mode:
```bash
kubectl get configmap media-cleanup-config -n media -o yaml | grep DRY_RUN

# If "true", change to "false":
kubectl patch configmap media-cleanup-config -n media --type merge \
  -p '{"data":{"DRY_RUN":"false"}}'
```

---

## Security Considerations

✅ **API keys in Kubernetes Secrets** (not ConfigMaps)  
✅ **Network policies** restrict traffic between services  
✅ **RBAC** limits cleanup job permissions  
✅ **NFSv4** with proper export restrictions  
✅ **Dry-run mode** enabled by default  

See [PHASE5-HARDENING.md](media-pipeline/PHASE5-HARDENING.md) for complete security guide.

---

## Performance Tuning

### Network Bandwidth
- **Minimum**: 100 Mbps for 4K REMUX Direct Play
- **Recommended**: 1 Gbps wired Ethernet
- **Not recommended**: Wi-Fi for 4K streaming

### Storage
- **Recommended**: SSD for application configs (Radarr, Sonarr, Jellyfin)
- **Acceptable**: HDD for media content (sequential reads)
- **NFS**: NFSv4 with jumbo frames (MTU 9000)

### Resource Limits
Current pod resource limits:
- **qBittorrent**: 2 CPU / 2 GB RAM
- **Radarr**: 1 CPU / 1 GB RAM
- **Sonarr**: 1 CPU / 1 GB RAM
- **Jellyfin**: 4 CPU / 4 GB RAM (for transcoding fallback)

Adjust based on workload in respective YAML files.

---

## Future Enhancements

**Not yet implemented but documented for reference**:

1. **Smart Season Packing**: Download remaining episodes to enable full-season deletion
2. **Stale Content Detection**: Delete unwatched content after X months
3. **Multi-Quality Handling**: Keep higher quality if multiple exist
4. **Prometheus Metrics**: Expose cleanup metrics for monitoring
5. **Grafana Dashboards**: Visualize cleanup stats and trends
6. **Quality Upgrade Automation**: Automatically upgrade to better releases

---

## Support & Documentation

- **Deployment**: [clusters/microk8s/media/README.md](clusters/microk8s/media/README.md)
- **API Reference**: [media-pipeline/API-JELLYFIN.md](media-pipeline/API-JELLYFIN.md), [media-pipeline/API-RADARR-SONARR.md](media-pipeline/API-RADARR-SONARR.md)
- **Series Logic**: [media-pipeline/PHASE4-SERIES-LOGIC.md](media-pipeline/PHASE4-SERIES-LOGIC.md)
- **Production Guide**: [media-pipeline/PHASE5-HARDENING.md](media-pipeline/PHASE5-HARDENING.md)

---

## License & Disclaimer

This is a technical implementation for educational purposes. Content acquisition methods are the user's responsibility. Ensure compliance with local laws and service terms.
