# Phase 2 Implementation — Home Assistant Hardening

**Status:** Complete  
**Date:** December 17, 2025

---

## Objective

**Goal:** Stable HA core that runs without supervisor, with externalized services and proper isolation.

---

## Changes Made

### 1. Removed USB Device Mount from Home Assistant Pod ✅

**File Modified:** [charts/home-assistant/templates/deployment.yaml](charts/home-assistant/templates/deployment.yaml)

**Changes:**
- Removed `/dev/ttyUSB0` volumeMount from Home Assistant container
- Removed `usb-devices` hostPath volume definition

**Rationale:** 
- USB Zigbee coordinator should only be mounted to Zigbee2MQTT pod (Phase 5)
- Home Assistant communicates with Zigbee devices via MQTT, not direct USB
- Reduces privilege requirements and improves isolation
- Prevents USB device contention between multiple pods

**Impact:** Home Assistant can no longer directly access USB devices. Zigbee functionality will be restored in Phase 5 via Zigbee2MQTT and MQTT broker.

---

### 2. Isolated Matter Server to Separate Deployment ✅

**Files Created:**
- [clusters/microk8s/apps/matter-server/namespace.yaml](clusters/microk8s/apps/matter-server/namespace.yaml)
- [clusters/microk8s/apps/matter-server/pvc.yaml](clusters/microk8s/apps/matter-server/pvc.yaml)
- [clusters/microk8s/apps/matter-server/deployment.yaml](clusters/microk8s/apps/matter-server/deployment.yaml)
- [clusters/microk8s/apps/matter-server/kustomization.yaml](clusters/microk8s/apps/matter-server/kustomization.yaml)

**File Modified:** 
- [charts/home-assistant/templates/deployment.yaml](charts/home-assistant/templates/deployment.yaml) — Removed Matter sidecar container
- [charts/home-assistant/templates/service.yaml](charts/home-assistant/templates/service.yaml) — Removed Matter port 5580
- [clusters/microk8s/apps/kustomization.yaml](clusters/microk8s/apps/kustomization.yaml) — Added matter-server

**Configuration:**
- **Namespace:** `matter` (dedicated namespace)
- **Image:** `ghcr.io/home-assistant-libs/python-matter-server:6.6.0` (version pinned)
- **Storage:** 1Gi NAS-backed PVC (`nfs-storage`)
- **Resources:** 50m-200m CPU, 128Mi-512Mi memory
- **Service:** ClusterIP on port 5580
- **Health Probes:** TCP socket checks on port 5580
- **Annotations:** Marked as `experimental: true`

**Rationale:**
- Matter crashes no longer affect Home Assistant stability
- Isolated namespace allows easy disabling (delete namespace)
- Separate resource limits and monitoring
- Can be upgraded/restarted independently
- Architecture requirement: "runs isolated in its own pod"

**Connection:** Matter server is accessible at `ws://matter-server.matter.svc.cluster.local:5580/ws`

---

### 3. Fixed Hardcoded Namespace in Helm Templates ✅

**File Modified:** [charts/home-assistant/templates/deployment.yaml](charts/home-assistant/templates/deployment.yaml)

Changed from:
```yaml
metadata:
  name: home-assistant
  namespace: home-automation
```

To:
```yaml
metadata:
  name: home-assistant
  namespace: {{ .Release.Namespace }}
```

**Rationale:** 
- Improves Helm chart reusability
- Follows Helm best practices
- Allows deployment to different namespaces if needed

---

### 4. Documented Privileged Mode Necessity ✅

**File Modified:** [charts/home-assistant/templates/deployment.yaml](charts/home-assistant/templates/deployment.yaml)

**Decision:** Keep `privileged: true` for now with documentation

**Reason:**
- Required for Bluetooth access (D-Bus socket and `/sys/class/bluetooth`)
- Bluetooth integrations need host-level access
- Alternative would require specific capabilities (`CAP_NET_ADMIN`, `CAP_NET_RAW`)

**Added Comment:**
```yaml
securityContext:
  # Privileged mode required for Bluetooth access (D-Bus and /sys/class/bluetooth)
  # TODO: Replace with specific capabilities (CAP_NET_ADMIN, CAP_NET_RAW) if Bluetooth
  # can be configured to work without full privileged mode
  privileged: true
```

**Future Improvement:** Test if Bluetooth works with reduced privileges in a future phase.

---

### 5. Updated Home Assistant Configuration ✅

**File Modified:** [charts/home-assistant/values.yaml](charts/home-assistant/values.yaml)

**Changes:**
- Added Matter integration documentation comments
- Provided connection URL for Matter server
- Included setup instructions

**Connection Details:**
```yaml
# Matter integration (experimental)
# Matter server runs in separate namespace for isolation
# Connection: ws://matter-server.matter.svc.cluster.local:5580/ws
# To configure Matter integration, go to:
# Configuration -> Integrations -> Add Integration -> Matter (BETA)
# Use the URL above when prompted for the Matter server address
```

---

## Architecture Compliance

### Critical Rules Status

| Rule | Requirement | Status | Notes |
|------|-------------|--------|-------|
| No Supervisor | HA Core only, no supervisor | ✅ | Using homeassistant/home-assistant image |
| No Add-ons | No HA add-ons | ✅ | Services in separate pods |
| USB Single Mount | USB only on one pod | ✅ | Removed from HA, ready for Zigbee2MQTT in Phase 5 |
| PostgreSQL | Use PostgreSQL recorder | ✅ | Completed in Phase 1 |
| Resource Limits | CPU/memory limits | ✅ | All pods have limits |
| Restart-Safe | No data loss on restart | ✅ | NAS-backed PVCs |

### Phase 2 Goals Achieved

✅ **Stable HA core** — Matter isolation prevents crashes from affecting HA  
✅ **Externalized services** — Matter runs separately, Zigbee/MQTT ready for Phase 5  
✅ **HA config persistent** — 5Gi NAS-backed PVC  
✅ **Reduced coupling** — HA no longer depends on sidecar containers  
✅ **Improved isolation** — Dedicated namespaces for services  

---

## Deployment Instructions

### Step 1: Commit and Push Changes

```powershell
git add .
git commit -m "Phase 2: Harden Home Assistant - isolate Matter, remove USB mount"
git push
```

### Step 2: Monitor Flux Reconciliation

```powershell
# Watch Flux sync
flux get kustomizations --watch

# Check Matter namespace creation
kubectl get namespace matter

# Check Matter deployment
kubectl get pods -n matter
```

### Step 3: Verify Matter Server

```powershell
# Check Matter pod status
kubectl get pods -n matter -l app=matter-server

# Check Matter logs
kubectl logs -n matter -l app=matter-server --tail=50

# Verify service endpoint
kubectl get svc -n matter
```

### Step 4: Restart Home Assistant

```powershell
# Restart HA to apply changes (remove sidecar, remove USB mount)
kubectl rollout restart deployment/home-assistant -n home-automation

# Watch the restart
kubectl rollout status deployment/home-assistant -n home-automation

# Check HA logs for errors
kubectl logs -n home-automation -l app.kubernetes.io/name=home-assistant --tail=100
```

### Step 5: Configure Matter Integration in Home Assistant

1. Open Home Assistant UI: `https://ha.gcelik.dev`
2. Go to **Configuration** → **Integrations**
3. Click **Add Integration**
4. Search for **Matter (BETA)**
5. When prompted for Matter server URL, enter: `ws://matter-server.matter.svc.cluster.local:5580/ws`
6. Complete setup wizard

---

## Testing & Verification

### Test 1: Home Assistant Stability

```powershell
# Verify HA is running without errors
kubectl get pods -n home-automation

# Check no USB-related errors
kubectl logs -n home-automation -l app.kubernetes.io/name=home-assistant | grep -i usb
```

Expected: No USB device errors (since USB was removed).

### Test 2: Matter Server Isolation

```powershell
# Crash Matter server intentionally
kubectl delete pod -n matter -l app=matter-server

# Verify HA continues running
kubectl get pods -n home-automation
```

Expected: Home Assistant pod remains running and healthy.

### Test 3: Matter Server Recovery

```powershell
# Wait for Matter pod to restart
kubectl get pods -n matter --watch

# Verify Matter reconnects
kubectl logs -n matter -l app=matter-server --tail=20
```

Expected: Matter server restarts automatically, HA reconnects if integration is configured.

### Test 4: Resource Usage

```powershell
# Check resource consumption
kubectl top pods -n home-automation
kubectl top pods -n matter
```

Expected: Lower resource usage in home-automation (no Matter sidecar).

---

## Breaking Changes

### ⚠️ USB Device Access Removed

**Impact:** If you were using USB devices directly in Home Assistant (Zigbee coordinator, Z-Wave stick), they will no longer work.

**Resolution:** Wait for Phase 5 when Zigbee2MQTT is deployed with proper USB mounting.

**Temporary Workaround:** If you need immediate USB access, you can temporarily revert this change:

```powershell
git revert HEAD
git push
```

### ⚠️ Matter Integration Requires Reconfiguration

**Impact:** Existing Matter integration in Home Assistant will break because sidecar is removed.

**Resolution:**
1. Remove old Matter integration from HA UI (if exists)
2. Wait for Matter server pod to start
3. Re-add Matter integration using new WebSocket URL
4. Re-pair Matter devices if necessary

---

## Rollback Plan

### Quick Rollback (Matter Only)

```powershell
# Disable Matter namespace
kubectl delete namespace matter

# HA continues working without Matter
```

### Full Rollback

```powershell
# Revert all Phase 2 changes
git revert HEAD
git push

# Restart Home Assistant
kubectl rollout restart deployment/home-assistant -n home-automation
```

---

## Storage Impact

**New Storage Allocated:**
- Matter Server PVC: 1Gi (NAS-backed)

**Total NAS Storage (All Services):** 48Gi
- Home Assistant: 5Gi
- InfluxDB: 20Gi
- Loki: 10Gi
- Grafana: 2Gi
- PostgreSQL: 10Gi
- Matter: 1Gi

---

## Next Steps

**Phase 3:** Long-Term Metrics (InfluxDB)
- Extend InfluxDB retention policy from 30 days to multi-year
- Configure appropriate shard duration for Raspberry Pi
- Document expected storage growth

**Phase 5 (After Phase 3 & 4):** Zigbee, Matter & Edge Integrations
- Deploy Mosquitto MQTT broker in `zigbee` namespace
- Deploy Zigbee2MQTT with USB device mount
- Configure Home Assistant to use MQTT for Zigbee
- Finalize Matter integration setup

---

## Files Changed Summary

```
Modified:
  charts/home-assistant/templates/deployment.yaml
  charts/home-assistant/templates/service.yaml
  charts/home-assistant/values.yaml
  clusters/microk8s/apps/kustomization.yaml

Created:
  clusters/microk8s/apps/matter-server/namespace.yaml
  clusters/microk8s/apps/matter-server/pvc.yaml
  clusters/microk8s/apps/matter-server/deployment.yaml
  clusters/microk8s/apps/matter-server/kustomization.yaml
  PHASE2_IMPLEMENTATION.md
```

---

*Phase 2 implementation complete. Home Assistant is now hardened with isolated services and ready for Phase 3.*
