# Media Pipeline - Phase 1 Deployment Guide

## Overview
This implements the core media pipeline infrastructure on Kubernetes with:
- **qBittorrent**: Download client (assumes VPN configured externally)
- **Radarr**: Movie management
- **Sonarr**: TV series management
- **Jellyfin**: Media streaming server

## Prerequisites

### NAS Configuration
Update the NFS server IP and paths in [storage.yaml](storage.yaml):
```yaml
nfs:
  server: 192.168.2.200        # Your NAS IP
  path: /volume1/media         # Movies/TV storage
  path: /volume1/downloads     # Download working directory
  path: /volume1/k8s-media-config  # App configurations
```

### Storage Structure
```
/volume1/media/
  ├── movies/           # Radarr managed
  └── tv/              # Sonarr managed

/volume1/downloads/
  ├── movies/          # Radarr download category
  ├── tv/             # Sonarr download category
  └── complete/       # qBittorrent completed

/volume1/k8s-media-config/
  ├── qbittorrent/
  ├── radarr/
  ├── sonarr/
  └── jellyfin/
```

## Deployment Steps

### 1. Deploy Infrastructure
```bash
kubectl apply -k clusters/microk8s/media/
```

Verify pods are running:
```bash
kubectl get pods -n media
```

### 2. Configure qBittorrent

Access qBittorrent WebUI (port-forward or ingress):
```bash
kubectl port-forward -n media svc/qbittorrent 8080:8080
```

Navigate to `http://localhost:8080`:
- Default credentials: `admin` / `adminadmin`
- **Change password immediately**
- Settings > Downloads:
  - Default Save Path: `/downloads/complete`
  - Keep incomplete torrents in: `/downloads/incomplete`
- Settings > BitTorrent:
  - Enable DHT, PeX, LSD
  - Encryption: Require encryption
- Settings > Web UI:
  - Enable CSRF protection
  - Optional: Enable authentication bypass for localhost

### 3. Configure Radarr

Port-forward:
```bash
kubectl port-forward -n media svc/radarr 7878:7878
```

Navigate to `http://localhost:7878`:

#### a) Download Client
- Settings > Download Clients > Add qBittorrent
  - Host: `qbittorrent.media.svc.cluster.local`
  - Port: `8080`
  - Username: `admin`
  - Password: (your qBittorrent password)
  - Category: `movies`
  - Priority: `1`
  
#### b) Root Folder
- Settings > Media Management > Root Folders
  - Add: `/movies`

#### c) Quality Profile
- Settings > Profiles > Add Quality Profile
  - Name: `Ultra-HD REMUX`
  - Allowed Qualities (in order):
    - Bluray-2160p Remux (cutoff)
    - Bluray-2160p
    - WEBDL-2160p
    - WEBRip-2160p
  - Upgrade Until: `Bluray-2160p Remux`

#### d) Custom Formats (for Atmos)
- Settings > Custom Formats > Add
  - **TrueHD Atmos**:
    - Release Title: `/TrueHD.*Atmos|Atmos.*TrueHD/i`
    - Score: `500`
  - **DTS-X**:
    - Release Title: `/DTS[-\.]?X/i`
    - Score: `450`
  - **DD+ Atmos**:
    - Release Title: `/DD[P+].*Atmos|Atmos.*DD[P+]/i`
    - Score: `300`
  - **REMUX**:
    - Release Title: `/\bREMUX\b/i`
    - Score: `200`

#### e) Quality Profile + Custom Formats
- Settings > Profiles > Edit `Ultra-HD REMUX`
  - Minimum Custom Format Score: `0`
  - Upgrade Until Custom Format Score: `10000`
  - Add all custom formats created above

### 4. Configure Sonarr

Port-forward:
```bash
kubectl port-forward -n media svc/sonarr 8989:8989
```

Navigate to `http://localhost:8989`:

#### Configuration (same as Radarr)
- Download Client: qBittorrent with category `tv`
- Root Folder: `/tv`
- Quality Profile: `Ultra-HD REMUX` (same custom formats as Radarr)
- Series Type: `Standard` for most shows, `Anime` for anime

### 5. Configure Jellyfin

Port-forward:
```bash
kubectl port-forward -n media svc/jellyfin 8096:8096
```

Navigate to `http://localhost:8096`:

#### Initial Setup
- Language: Select your language
- Create admin user
- Add Media Libraries:
  - **Movies**: `/media/movies`
  - **TV Shows**: `/media/tv`

#### Critical Settings for Direct Play / No Transcoding

**Dashboard > Playback**:
- ✅ Allow video playback that requires conversion without re-encoding
- ❌ Allow audio playback that requires conversion
- ❌ Allow media conversion
- Preferred video codec: `Auto`
- Hardware acceleration: `None` (no transcoding)

**Dashboard > Transcoding**:
- Transcoding thread count: `1` (minimize impact if fallback happens)
- Transcode path: `/cache/transcoding-temp`
- ❌ Enable VPP Tone mapping
- ❌ Enable hardware decoding

**Dashboard > Networking**:
- Enable automatic port mapping: Disable (use Kubernetes services)
- Local network addresses: `10.0.0.0/8,172.16.0.0/12,192.168.0.0/16`

**Client Settings** (per device in playback settings):
- Video quality: `Maximum`
- Audio: `Auto` (will prefer direct play)
- Subtitle mode: `Only forced subtitles` (avoids video transcoding)

### 6. Retrieve API Keys

Store API keys as Kubernetes secret:

```bash
# Get Radarr API key
RADARR_KEY=$(kubectl exec -n media deployment/radarr -- cat /config/config.xml | grep -oPm1 "(?<=<ApiKey>)[^<]+")

# Get Sonarr API key
SONARR_KEY=$(kubectl exec -n media deployment/sonarr -- cat /config/config.xml | grep -oPm1 "(?<=<ApiKey>)[^<]+")

# Get Jellyfin API key (create in Dashboard > API Keys)
# Create via UI first, then retrieve
JELLYFIN_KEY="YOUR_JELLYFIN_API_KEY"

# Get qBittorrent password
QBITTORRENT_PASS="YOUR_QB_PASSWORD"

# Create secret
kubectl create secret generic media-api-keys -n media \
  --from-literal=radarr-api-key="$RADARR_KEY" \
  --from-literal=sonarr-api-key="$SONARR_KEY" \
  --from-literal=jellyfin-api-key="$JELLYFIN_KEY" \
  --from-literal=qbittorrent-password="$QBITTORRENT_PASS"
```

## Verification

### Test Download Flow
1. Add a movie in Radarr
2. Verify it appears in qBittorrent with category `movies`
3. After download completes, verify file in `/movies/`
4. Verify Jellyfin detects the new movie

### Test Direct Play
1. Play a 2160p REMUX file in Jellyfin
2. During playback, check Dashboard > Activity:
   - **Direct Play**: ✅ Success (no transcoding)
   - **Transcode**: ❌ Indicates configuration issue

Check session details:
```
Video: Direct Play (HEVC Main 10)
Audio: Direct Play (TrueHD)
Container: Direct Play
```

If transcoding occurs, common issues:
- Client doesn't support codec (check client device capabilities)
- Network bandwidth too low (need 100+ Mbps for 4K REMUX)
- Subtitle rendering (disable subtitles or use external file)

## Audio Codec Preservation

**Why this matters**: TrueHD Atmos and DTS-HD MA require bitstream passthrough. Any transcoding destroys the lossless audio and Atmos metadata.

**Requirements**:
- Client device with HDMI eARC/ARC to AVR
- AVR/Soundbar supporting TrueHD Atmos / DTS:X
- Network: 100+ Mbps for reliable 4K REMUX streaming
- Jellyfin client app that supports audio passthrough (official apps do)

**Verification**:
Play a movie with TrueHD Atmos and check your AVR display:
- Should show: `Dolby Atmos` or `TrueHD Atmos`
- If showing: `DD 5.1` or `PCM` = audio was transcoded

## Network Access (Optional)

To access services via Ingress, create ingress resources or use existing ingress-nginx:

```yaml
# Example ingress for Jellyfin
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: jellyfin
  namespace: media
spec:
  rules:
  - host: jellyfin.yourdomain.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: jellyfin
            port:
              number: 8096
```

## Next Steps
- **Phase 2**: API integration and watched state detection
- **Phase 3**: Automated cleanup implementation
- **Phase 4**: Series-safe deletion logic
- **Phase 5**: Hardening and production readiness
