# Phase 5 Implementation — Zigbee, Matter & Edge Integrations

**Status:** Complete  
**Date:** December 17, 2025

---

## Objective

**Goal:** Hardware stability with isolated Zigbee2MQTT, MQTT broker, and finalized Matter integration.

---

## Changes Made

### 1. Created Zigbee Namespace ✅

**File Created:** [clusters/microk8s/apps/zigbee/namespace.yaml](clusters/microk8s/apps/zigbee/namespace.yaml)

**Rationale:**
- Dedicated namespace for Zigbee services (architecture requirement)
- Isolates Zigbee/MQTT from Home Assistant
- Enables independent scaling and monitoring
- Follows microservices best practices

---

### 2. Deployed Mosquitto MQTT Broker ✅

**Files Created:**
- [clusters/microk8s/apps/zigbee/mosquitto-config.yaml](clusters/microk8s/apps/zigbee/mosquitto-config.yaml)
- [clusters/microk8s/apps/zigbee/mosquitto-deployment.yaml](clusters/microk8s/apps/zigbee/mosquitto-deployment.yaml)
- [clusters/microk8s/apps/zigbee/pvc.yaml](clusters/microk8s/apps/zigbee/pvc.yaml)

**Configuration:**
- **Image:** `eclipse-mosquitto:2.0.18` (stable version)
- **Storage:** 1Gi NAS-backed PVC (`mosquitto-data`)
- **Ports:** 1883 (MQTT), 9001 (WebSocket)
- **Authentication:** Username/password (no anonymous access)
- **Users:** `zigbee2mqtt`, `homeassistant`
- **ACL:** Topic-based access control
- **Persistence:** Enabled for message retention
- **Resources:** 50m-200m CPU, 64Mi-256Mi memory

**Security Features:**
- Password file with hashed credentials (mosquitto_passwd)
- ACL restricts topic access by user
- Init container generates password file from Sealed Secrets
- No anonymous connections allowed

**ACL Configuration:**
```
zigbee2mqtt user:
  - readwrite: zigbee2mqtt/#
  - read: homeassistant/#

homeassistant user:
  - readwrite: # (all topics)
```

**Rationale:**
- MQTT broker required for Zigbee2MQTT ↔ Home Assistant communication
- Separate broker allows independent upgrades/restarts
- Persistent storage ensures message queue survives restarts
- WebSocket support enables web-based MQTT clients
- NAS-backed persistence prevents data loss

---

### 3. Deployed Zigbee2MQTT ✅

**Files Created:**
- [clusters/microk8s/apps/zigbee/zigbee2mqtt-config.yaml](clusters/microk8s/apps/zigbee/zigbee2mqtt-config.yaml)
- [clusters/microk8s/apps/zigbee/zigbee2mqtt-deployment.yaml](clusters/microk8s/apps/zigbee/zigbee2mqtt-deployment.yaml)

**Configuration:**
- **Image:** `koenkk/zigbee2mqtt:1.39.1` (version pinned)
- **Storage:** 1Gi NAS-backed PVC (`zigbee2mqtt-data`)
- **USB Device:** `/dev/ttyUSB0` (Zigbee coordinator)
- **MQTT:** Connects to `mosquitto.zigbee.svc.cluster.local:1883`
- **Home Assistant Integration:** Enabled with MQTT discovery
- **Frontend:** Port 8080 with token authentication
- **Resources:** 100m-500m CPU, 128Mi-512Mi memory
- **Security:** Privileged mode for USB access

**Key Features:**
- Home Assistant discovery enabled
- Frontend for device pairing and management
- Persistent device/group configuration
- Auto-generated network key and PAN ID
- Channel 11 (default, least congested)
- Device availability monitoring (300s timeout)
- Cache state for faster startup

**USB Device Mounting:**
```yaml
volumes:
- name: usb-device
  hostPath:
    path: /dev/ttyUSB0
    type: CharDevice

volumeMounts:
- name: usb-device
  mountPath: /dev/ttyUSB0

securityContext:
  privileged: true  # Required for USB access
```

**Rationale:**
- USB device mounted ONLY to Zigbee2MQTT (architecture requirement)
- Home Assistant communicates via MQTT (no direct USB access)
- Privileged mode necessary for hardware access
- Persistent storage preserves device pairings across restarts
- Version pinning prevents unexpected behavior changes

---

### 4. Configured Home Assistant MQTT Integration ✅

**File Modified:** [charts/home-assistant/values.yaml](charts/home-assistant/values.yaml)

**MQTT Configuration Added:**
```yaml
mqtt:
  broker: mosquitto.zigbee.svc.cluster.local
  port: 1883
  username: homeassistant
  password: !secret mqtt_password
  discovery: true
  discovery_prefix: homeassistant
  birth_message:
    topic: 'homeassistant/status'
    payload: 'online'
  will_message:
    topic: 'homeassistant/status'
    payload: 'offline'
```

**File Modified:** [charts/home-assistant/templates/deployment.yaml](charts/home-assistant/templates/deployment.yaml)

**Changes:**
- Added `mqtt_password` to `secrets.yaml` template
- Added `MQTT_PASSWORD` environment variable from `home-assistant-config` secret

**Rationale:**
- MQTT discovery enables automatic device detection
- Birth/will messages track Home Assistant connectivity
- Credentials stored in Sealed Secret (security best practice)
- Broker DNS name uses Kubernetes service discovery

---

### 5. Created Sealed Secrets for MQTT ✅

**File Created:** [clusters/microk8s/apps/zigbee/sealed-secrets.yaml](clusters/microk8s/apps/zigbee/sealed-secrets.yaml)

**Secrets Required:**

**1. `mqtt-credentials` (namespace: zigbee)**
- `zigbee2mqtt-password` — Zigbee2MQTT → Mosquitto authentication
- `homeassistant-password` — Home Assistant → Mosquitto authentication

**2. `zigbee2mqtt-credentials` (namespace: zigbee)**
- `frontend-auth-token` — Zigbee2MQTT frontend authentication

**3. `home-assistant-config` (namespace: home-automation) — Updated**
- `mqtt-password` — Added to existing secret

**Rationale:**
- Separate secrets per namespace for isolation
- Prevents credential exposure in Git
- Enables credential rotation without code changes
- Follows security best practices

---

### 6. Updated Kustomization ✅

**File Modified:** [clusters/microk8s/apps/kustomization.yaml](clusters/microk8s/apps/kustomization.yaml)

Added `zigbee/` to resources list.

---

## Architecture Compliance

### Critical Requirements ✅

| Requirement | Status | Implementation |
|-------------|--------|----------------|
| Zigbee2MQTT in own namespace | ✅ | `zigbee` namespace created |
| USB mounted ONLY to Zigbee2MQTT | ✅ | `/dev/ttyUSB0` in Zigbee2MQTT pod |
| MQTT broker runs separately | ✅ | Mosquitto in `zigbee` namespace |
| HA communicates via MQTT only | ✅ | MQTT integration configured |
| Matter in isolated pod | ✅ | Completed in Phase 2 |
| Matter crashes don't affect HA | ✅ | Separate `matter` namespace |

### Service Isolation

```
┌─────────────────────────────────────────────┐
│  home-automation namespace                  │
│  ┌─────────────────────────────────────┐   │
│  │  Home Assistant                     │   │
│  │  - No USB device                    │   │
│  │  - MQTT client only                 │   │
│  └─────────────────────────────────────┘   │
└─────────────────────────────────────────────┘
              │
              │ MQTT (port 1883)
              ▼
┌─────────────────────────────────────────────┐
│  zigbee namespace                           │
│  ┌──────────────────┐  ┌─────────────────┐ │
│  │  Mosquitto MQTT  │  │  Zigbee2MQTT    │ │
│  │  - Broker        │◄─┤  - USB: ttyUSB0 │ │
│  │  - Port 1883     │  │  - Coordinator  │ │
│  └──────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────┘

┌─────────────────────────────────────────────┐
│  matter namespace                           │
│  ┌─────────────────────────────────────┐   │
│  │  Matter Server                      │   │
│  │  - Port 5580 (WebSocket)            │   │
│  │  - Isolated from HA                 │   │
│  └─────────────────────────────────────┘   │
└─────────────────────────────────────────────┘
```

---

## Storage Allocation

**New PVCs Created:**

| PVC | Namespace | Size | Purpose |
|-----|-----------|------|---------|
| `mosquitto-data` | zigbee | 1Gi | MQTT message persistence |
| `zigbee2mqtt-data` | zigbee | 1Gi | Device configs, network state |

**Total NAS Storage (All Services):** 50Gi
- Home Assistant: 5Gi
- InfluxDB: 20Gi
- Loki: 10Gi
- Grafana: 2Gi
- PostgreSQL: 10Gi
- Matter: 1Gi
- Mosquitto: 1Gi
- Zigbee2MQTT: 1Gi

---

## Deployment Instructions

### Step 1: Generate MQTT Sealed Secrets

```powershell
# Generate secure passwords
$zigbee2mqttPassword = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | % {[char]$_})
$homeassistantPassword = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | % {[char]$_})
$frontendToken = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 64 | % {[char]$_})

# Save passwords for later use
Write-Host "Zigbee2MQTT Password: $zigbee2mqttPassword"
Write-Host "Home Assistant MQTT Password: $homeassistantPassword"
Write-Host "Zigbee2MQTT Frontend Token: $frontendToken"

# Create MQTT credentials sealed secret
@"
apiVersion: v1
kind: Secret
metadata:
  name: mqtt-credentials
  namespace: zigbee
type: Opaque
stringData:
  zigbee2mqtt-password: $zigbee2mqttPassword
  homeassistant-password: $homeassistantPassword
"@ | kubectl create --dry-run=client -o yaml -f - | kubeseal --format=yaml > clusters/microk8s/apps/zigbee/sealed-secrets-mqtt.yaml

# Create Zigbee2MQTT frontend credentials sealed secret
@"
apiVersion: v1
kind: Secret
metadata:
  name: zigbee2mqtt-credentials
  namespace: zigbee
type: Opaque
stringData:
  frontend-auth-token: $frontendToken
"@ | kubectl create --dry-run=client -o yaml -f - | kubeseal --format=yaml >> clusters/microk8s/apps/zigbee/sealed-secrets-frontend.yaml

# Combine into single file
cat clusters/microk8s/apps/zigbee/sealed-secrets-mqtt.yaml > clusters/microk8s/apps/zigbee/sealed-secrets.yaml
echo "---" >> clusters/microk8s/apps/zigbee/sealed-secrets.yaml
cat clusters/microk8s/apps/zigbee/sealed-secrets-frontend.yaml >> clusters/microk8s/apps/zigbee/sealed-secrets.yaml

# Clean up temporary files
rm clusters/microk8s/apps/zigbee/sealed-secrets-mqtt.yaml
rm clusters/microk8s/apps/zigbee/sealed-secrets-frontend.yaml
```

### Step 2: Update Home Assistant Sealed Secret

```powershell
# Use the same password generated for Home Assistant
$homeassistantPassword = "YOUR_HA_MQTT_PASSWORD_FROM_STEP1"

# Add mqtt-password to existing home-assistant-config sealed secret
kubectl create secret generic home-assistant-config \
  --from-literal=mqtt-password="$homeassistantPassword" \
  --dry-run=client -o yaml | kubeseal --format=yaml --merge-into clusters/microk8s/apps/home-assistant/sealed-secret.yaml
```

### Step 3: Commit and Push Changes

```powershell
git add .
git commit -m "Phase 5: Deploy Zigbee2MQTT, MQTT broker, and integrate with HA"
git push
```

### Step 4: Monitor Deployment

```powershell
# Watch Flux reconciliation
flux get kustomizations --watch

# Check zigbee namespace creation
kubectl get namespace zigbee

# Check Mosquitto deployment
kubectl get pods -n zigbee -l app=mosquitto --watch

# Check Zigbee2MQTT deployment
kubectl get pods -n zigbee -l app=zigbee2mqtt --watch
```

### Step 5: Verify MQTT Broker

```powershell
# Check Mosquitto logs
kubectl logs -n zigbee -l app=mosquitto --tail=50

# Expected: Mosquitto started, listening on 1883 and 9001

# Verify service
kubectl get svc -n zigbee mosquitto
```

### Step 6: Verify Zigbee2MQTT

```powershell
# Check Zigbee2MQTT logs
kubectl logs -n zigbee -l app=zigbee2mqtt --tail=100

# Expected:
# - Connected to MQTT broker
# - Zigbee coordinator initialized
# - Listening on port 8080

# Check USB device detection
kubectl logs -n zigbee -l app=zigbee2mqtt | grep -i "serial port"

# Expected: Serial port /dev/ttyUSB0 opened
```

### Step 7: Restart Home Assistant

```powershell
# Restart to apply MQTT configuration
kubectl rollout restart deployment/home-assistant -n home-automation

# Monitor restart
kubectl rollout status deployment/home-assistant -n home-automation

# Check logs for MQTT connection
kubectl logs -n home-automation -l app.kubernetes.io/name=home-assistant --tail=100 | grep -i mqtt

# Expected: Successfully connected to MQTT broker
```

### Step 8: Access Zigbee2MQTT Frontend

```powershell
# Port-forward to access frontend
kubectl port-forward -n zigbee svc/zigbee2mqtt 8080:8080
```

Open browser: `http://localhost:8080`

Login with frontend auth token (from Step 1).

### Step 9: Pair Zigbee Devices

1. In Zigbee2MQTT frontend, click **"Permit Join"**
2. Put Zigbee device in pairing mode (refer to device manual)
3. Wait for device to appear in Zigbee2MQTT
4. Rename device with friendly name
5. Device automatically appears in Home Assistant (MQTT discovery)

---

## Testing & Validation

### Test 1: MQTT Broker Connectivity

```powershell
# Install mosquitto-clients (if not installed)
# Ubuntu/Debian: apt-get install mosquitto-clients
# macOS: brew install mosquitto

# Port-forward MQTT broker
kubectl port-forward -n zigbee svc/mosquitto 1883:1883

# Subscribe to Zigbee2MQTT topics
mosquitto_sub -h localhost -p 1883 -u homeassistant -P "YOUR_PASSWORD" -t "zigbee2mqtt/#" -v

# In another terminal, publish test message
mosquitto_pub -h localhost -p 1883 -u homeassistant -P "YOUR_PASSWORD" -t "zigbee2mqtt/bridge/info" -m '{"type":"request"}'
```

Expected: Test message appears in subscriber terminal.

### Test 2: Zigbee Coordinator Detection

```powershell
# Check Zigbee2MQTT detected USB device
kubectl logs -n zigbee -l app=zigbee2mqtt | grep -A5 "Coordinator"

# Expected output similar to:
# Coordinator type: zStack3x0
# Coordinator firmware version: 20210708
```

### Test 3: Home Assistant MQTT Discovery

1. Open Home Assistant UI
2. Go to **Configuration** → **Integrations**
3. Look for **MQTT** integration
4. Should show "Discovered" or "Configured"
5. Click on MQTT integration
6. Verify broker: `mosquitto.zigbee.svc.cluster.local`

### Test 4: Device Pairing

1. Enable **Permit Join** in Zigbee2MQTT frontend
2. Pair a test device (e.g., Zigbee sensor)
3. Verify device appears in Zigbee2MQTT
4. Verify device auto-discovers in Home Assistant
5. Check Home Assistant → **Configuration** → **Devices**

### Test 5: Matter Server Isolation

```powershell
# Crash Matter server intentionally
kubectl delete pod -n matter -l app=matter-server

# Verify Home Assistant continues running
kubectl get pods -n home-automation

# Verify Zigbee2MQTT continues running
kubectl get pods -n zigbee
```

Expected: HA and Zigbee2MQTT remain healthy, Matter restarts automatically.

### Test 6: Zigbee2MQTT Crash Recovery

```powershell
# Crash Zigbee2MQTT
kubectl delete pod -n zigbee -l app=zigbee2mqtt

# Wait for restart
kubectl get pods -n zigbee --watch

# Verify devices reconnect
kubectl logs -n zigbee -l app=zigbee2mqtt --tail=50 | grep -i "device"
```

Expected: Zigbee2MQTT restarts, devices reconnect automatically, Home Assistant continues functioning.

---

## Zigbee Network Configuration

### Default Settings

- **Network Key:** Auto-generated (stored in `/data/configuration.yaml`)
- **PAN ID:** Auto-generated
- **Channel:** 11 (default)
- **Adapter:** Auto-detect

### Channel Selection

**Channel 11** is default. If you experience interference:

**Recommended Channels:**
- **11** — Least overlap with WiFi (if WiFi on channels 1-5)
- **15** — Middle ground
- **20** — Least overlap with WiFi (if WiFi on channels 6-11)
- **25** — Best for 2.4 GHz-free environments

**Avoid channels 12-14** in most regions (regulatory restrictions).

### Change Channel (if needed)

```powershell
# Edit Zigbee2MQTT config
kubectl edit configmap zigbee2mqtt-config -n zigbee

# Change:
# channel: 15

# Restart Zigbee2MQTT
kubectl rollout restart deployment/zigbee2mqtt -n zigbee

# WARNING: All devices must be re-paired after channel change!
```

---

## Zigbee Device Management

### Add Devices

1. Zigbee2MQTT Frontend → **Permit Join** (button turns green)
2. Put device in pairing mode (usually hold button 5-10 seconds)
3. Device appears in Zigbee2MQTT within 60 seconds
4. Rename device (e.g., "Living Room Sensor")
5. Device auto-discovers in Home Assistant

### Remove Devices

1. Zigbee2MQTT Frontend → Select device → **Remove**
2. Device automatically removed from Home Assistant

### Update Firmware (OTA)

1. Zigbee2MQTT Frontend → Device → **OTA Updates**
2. Click **Check for updates**
3. If available, click **Update**
4. Wait for update to complete (device may restart)

### Device Availability

Zigbee2MQTT monitors device availability (300s timeout).

**In Home Assistant:**
- Device shows "Available" or "Unavailable"
- Create automation for unavailability alerts

---

## MQTT Topics

### Zigbee2MQTT Topics

**Bridge Status:**
- `zigbee2mqtt/bridge/state` — online/offline
- `zigbee2mqtt/bridge/info` — Coordinator info
- `zigbee2mqtt/bridge/devices` — List of all devices

**Device Topics:**
- `zigbee2mqtt/[FRIENDLY_NAME]` — Device state (JSON)
- `zigbee2mqtt/[FRIENDLY_NAME]/set` — Command device
- `zigbee2mqtt/[FRIENDLY_NAME]/availability` — online/offline

### Home Assistant Topics

**Discovery:**
- `homeassistant/[COMPONENT]/[DEVICE_ID]/config` — Device config

**Status:**
- `homeassistant/status` — online/offline

---

## Troubleshooting

### Mosquitto Won't Start

```powershell
# Check logs
kubectl logs -n zigbee -l app=mosquitto --tail=100

# Common issues:
# - Sealed secret not created
# - Password file generation failed
# - PVC not bound

# Check secret
kubectl get secret mqtt-credentials -n zigbee

# Check PVC
kubectl get pvc -n zigbee mosquitto-data
```

### Zigbee2MQTT Can't Connect to MQTT

```powershell
# Check Zigbee2MQTT logs
kubectl logs -n zigbee -l app=zigbee2mqtt --tail=100 | grep -i "mqtt"

# Common issues:
# - Wrong password in sealed secret
# - Mosquitto not running
# - Secret not mounted correctly

# Verify secret
kubectl get secret zigbee2mqtt-credentials -n zigbee

# Test MQTT connection from Zigbee2MQTT pod
kubectl exec -it -n zigbee $(kubectl get pod -n zigbee -l app=zigbee2mqtt -o name) -- sh
# Inside pod:
apk add mosquitto-clients
mosquitto_pub -h mosquitto.zigbee.svc.cluster.local -p 1883 -u zigbee2mqtt -P "PASSWORD" -t "test" -m "hello"
```

### USB Device Not Found

```powershell
# Check Zigbee2MQTT logs
kubectl logs -n zigbee -l app=zigbee2mqtt --tail=100 | grep -i "serial"

# Error: "Error: Error while opening serialport 'Error: No such file or directory'"

# Check USB device on node
kubectl exec -it -n zigbee $(kubectl get pod -n zigbee -l app=zigbee2mqtt -o name) -- ls -l /dev/ttyUSB0

# If not found:
# 1. Verify USB device connected to pi-lab node
# 2. Check device permissions on host
# 3. Verify hostPath in deployment matches actual device
```

### Home Assistant Can't Connect to MQTT

```powershell
# Check HA logs
kubectl logs -n home-automation -l app.kubernetes.io/name=home-assistant --tail=100 | grep -i "mqtt"

# Common issues:
# - Wrong password in home-assistant-config secret
# - MQTT integration not loaded
# - Mosquitto not reachable

# Test MQTT from HA pod
kubectl exec -it -n home-automation $(kubectl get pod -n home-automation -l app.kubernetes.io/name=home-assistant -o name) -- sh
# Inside pod (if mosquitto-clients available):
mosquitto_sub -h mosquitto.zigbee.svc.cluster.local -p 1883 -u homeassistant -P "PASSWORD" -t "zigbee2mqtt/#"
```

### Devices Not Auto-Discovering

```powershell
# Check MQTT discovery enabled in HA
kubectl logs -n home-automation -l app.kubernetes.io/name=home-assistant | grep -i "mqtt.*discovery"

# Check Zigbee2MQTT sending discovery messages
kubectl exec -n zigbee -it $(kubectl get pod -n zigbee -l app=mosquitto -o name) -- sh
# Install mosquitto-clients and subscribe:
mosquitto_sub -h localhost -p 1883 -u homeassistant -P "PASSWORD" -t "homeassistant/#" -v

# Should see discovery messages when devices pair
```

---

## Performance & Resource Usage

### Expected Resource Consumption

**Mosquitto:**
- CPU: 20-50m (idle), 50-200m (active)
- Memory: 30-100 MB

**Zigbee2MQTT:**
- CPU: 50-150m (idle), 150-500m (pairing/updates)
- Memory: 80-200 MB

**Total Zigbee Stack:**
- CPU: 70-200m idle, 200-700m active
- Memory: 110-300 MB

**Acceptable on Raspberry Pi 4/5 (8GB RAM).**

### Device Limits

**Zigbee Network Capacity:**
- **Direct children:** 20-50 devices (depends on coordinator)
- **Total devices:** 100+ (with router devices)
- **Recommendation:** < 50 devices for stability on Raspberry Pi

**MQTT Message Throughput:**
- **Typical home:** 10-100 messages/second
- **Large home:** 100-500 messages/second
- **Mosquitto can handle:** 10,000+ messages/second

---

## Security Considerations

### MQTT Security

✅ **No anonymous access** — All clients require credentials  
✅ **ACL enforced** — Topic restrictions per user  
✅ **Passwords hashed** — mosquitto_passwd encryption  
✅ **Credentials in Sealed Secrets** — Encrypted at rest  

⚠️ **No TLS/SSL** — Internal cluster communication (acceptable)  
⚠️ **Mosquitto exposed internally only** — ClusterIP service  

**Future Enhancement:**
- Enable TLS for MQTT (optional, adds overhead)
- Add certificate-based authentication

### USB Device Security

⚠️ **Privileged mode required** — Zigbee2MQTT needs USB access  

**Mitigation:**
- USB device mounted ONLY to Zigbee2MQTT pod
- Zigbee2MQTT in isolated namespace
- Node selector pins pod to specific node

---

## Backup & Recovery

### What to Backup

**Zigbee2MQTT Data:**
- `/data/configuration.yaml` — Network key, PAN ID, channel
- `/data/devices.yaml` — Device pairings and friendly names
- `/data/groups.yaml` — Zigbee groups

**Mosquitto Data:**
- `/mosquitto/data/` — Persisted messages (if any)

**Home Assistant:**
- `secrets.yaml` — MQTT password

### Backup Strategy

**Automatic (via NAS PVC):**
- All data stored on NAS
- NAS snapshots backup everything

**Manual Backup:**
```powershell
# Backup Zigbee2MQTT data
kubectl cp zigbee/$(kubectl get pod -n zigbee -l app=zigbee2mqtt -o name | cut -d/ -f2):/data ./backup/zigbee2mqtt-data

# Backup includes network key — keep secure!
```

### Restore After Disaster

```powershell
# Restore Zigbee2MQTT data
kubectl cp ./backup/zigbee2mqtt-data zigbee/$(kubectl get pod -n zigbee -l app=zigbee2mqtt -o name | cut -d/ -f2):/data

# Restart Zigbee2MQTT
kubectl rollout restart deployment/zigbee2mqtt -n zigbee

# Devices reconnect automatically (no re-pairing needed)
```

---

## Rollback Plan

### Rollback to Phase 4 (No Zigbee)

```powershell
# Remove zigbee namespace
kubectl delete namespace zigbee

# Revert Home Assistant MQTT config
git revert HEAD
git push

# Restart Home Assistant
kubectl rollout restart deployment/home-assistant -n home-automation
```

**Warning:** Zigbee devices will stop working until Zigbee2MQTT is redeployed.

### Temporary Disable Zigbee2MQTT

```powershell
# Scale down Zigbee2MQTT (keep MQTT broker)
kubectl scale deployment zigbee2mqtt -n zigbee --replicas=0

# USB device becomes available again (not recommended for HA)
```

---

## Next Steps

### Phase 6: Backups & Disaster Recovery

Will add:
- Document backup locations (NAS paths)
- Provide restore procedures for all services
- Add PostgreSQL backup CronJob
- Create restore test procedure
- Document recovery time objectives (RTO)

---

## Summary

### Achievements ✅

✅ **Zigbee namespace created** — Dedicated namespace for Zigbee services  
✅ **Mosquitto deployed** — MQTT broker with authentication and ACL  
✅ **Zigbee2MQTT deployed** — USB coordinator, MQTT integration  
✅ **HA MQTT configured** — Automatic device discovery enabled  
✅ **USB isolation** — USB mounted ONLY to Zigbee2MQTT pod  
✅ **Matter finalized** — Isolated in separate namespace (Phase 2)  
✅ **Service isolation** — Crashes don't affect other services  
✅ **NAS-backed storage** — All data persistent (2Gi allocated)  

### Service Architecture

| Service | Namespace | USB Access | MQTT | Storage |
|---------|-----------|------------|------|---------|
| Home Assistant | home-automation | ❌ | Client | 5Gi |
| Zigbee2MQTT | zigbee | ✅ /dev/ttyUSB0 | Client | 1Gi |
| Mosquitto | zigbee | ❌ | Broker | 1Gi |
| Matter Server | matter | ❌ | ❌ | 1Gi |

### Files Changed Summary

```
Modified:
  charts/home-assistant/values.yaml
  charts/home-assistant/templates/deployment.yaml
  clusters/microk8s/apps/kustomization.yaml

Created:
  clusters/microk8s/apps/zigbee/namespace.yaml
  clusters/microk8s/apps/zigbee/pvc.yaml
  clusters/microk8s/apps/zigbee/mosquitto-config.yaml
  clusters/microk8s/apps/zigbee/mosquitto-deployment.yaml
  clusters/microk8s/apps/zigbee/zigbee2mqtt-config.yaml
  clusters/microk8s/apps/zigbee/zigbee2mqtt-deployment.yaml
  clusters/microk8s/apps/zigbee/sealed-secrets.yaml
  clusters/microk8s/apps/zigbee/kustomization.yaml
  PHASE5_IMPLEMENTATION.md
```

---

*Phase 5 implementation complete. Zigbee, MQTT, and Matter services fully isolated and integrated with Home Assistant.*
