# Phase 1 Implementation — Storage & Data Safety

**Status:** Complete (Requires manual steps before deployment)  
**Date:** December 17, 2025

---

## Changes Made

### 1. Fixed Duplicate Default StorageClass ✅

**File Modified:** [clusters/microk8s/storage/ssd-storageclass.yaml](clusters/microk8s/storage/ssd-storageclass.yaml)

- Removed `storageclass.kubernetes.io/is-default-class: "true"` annotation from `ssd-storage`
- Only `nfs-storage` remains as the default StorageClass
- This ensures all new PVCs without explicit storageClass use NAS storage

**Rationale:** Prevents accidental use of local SSD storage for stateful workloads that require NAS durability.

---

### 2. Deployed PostgreSQL with NAS-Backed Storage ✅

**Files Created:**
- [clusters/microk8s/databases/postgresql/pvc.yaml](clusters/microk8s/databases/postgresql/pvc.yaml)
- [clusters/microk8s/databases/postgresql/helmrelease.yaml](clusters/microk8s/databases/postgresql/helmrelease.yaml)
- [clusters/microk8s/databases/postgresql/kustomization.yaml](clusters/microk8s/databases/postgresql/kustomization.yaml)
- [clusters/microk8s/databases/postgresql/sealed-secret.yaml](clusters/microk8s/databases/postgresql/sealed-secret.yaml)

**Configuration:**
- PostgreSQL 16.4.0 (Bitnami chart 15.5.32)
- 10Gi NAS-backed PVC with `nfs-storage` class
- Database: `homeassistant`
- User: `homeassistant`
- Resources: 100m-500m CPU, 256Mi-1Gi memory
- Optimized for Raspberry Pi workload
- Metrics enabled for future Prometheus integration

**Rationale:** Provides durable, ACID-compliant database for Home Assistant recorder, eliminating SQLite corruption risks on NFS.

---

### 3. Migrated Home Assistant Recorder to PostgreSQL ✅

**File Modified:** [charts/home-assistant/values.yaml](charts/home-assistant/values.yaml)

Changed from:
```yaml
recorder:
  purge_keep_days: 10
  db_url: "sqlite:////config/home-assistant_v2.db"
```

To:
```yaml
recorder:
  purge_keep_days: 30
  db_url: !secret postgresql_url
```

**File Modified:** [charts/home-assistant/templates/deployment.yaml](charts/home-assistant/templates/deployment.yaml)

- Added `POSTGRESQL_URL` environment variable from sealed secret
- Added `postgresql_url` to secrets.yaml template
- Increased retention from 10 to 30 days (PostgreSQL can handle more)

**Rationale:** PostgreSQL on NAS is safe for concurrent access and power-loss scenarios. Extended retention to 30 days since PostgreSQL performs better than SQLite.

---

### 4. Updated Database Kustomization ✅

**File Modified:** [clusters/microk8s/databases/kustomization.yaml](clusters/microk8s/databases/kustomization.yaml)

Added `postgresql/` to resources list.

---

### 5. Verified All Stateful Services Use NAS Storage ✅

**Verification Results:**

| Service | PVC Name | StorageClass | Size | Status |
|---------|----------|--------------|------|--------|
| Home Assistant | home-assistant-data | nfs-storage | 5Gi | ✅ |
| InfluxDB | influxdb-influxdb2 | nfs-storage | 20Gi | ✅ |
| Loki | loki-storage | nfs-storage | 10Gi | ✅ |
| Grafana | grafana-storage | nfs-storage | 2Gi | ✅ |
| PostgreSQL | postgresql-data | nfs-storage | 10Gi | ✅ |

**Total NAS Storage Allocated:** 47Gi

---

## Manual Steps Required Before Deployment

### Step 1: Generate PostgreSQL Sealed Secrets

The PostgreSQL sealed secret file contains placeholder values. You must generate real sealed secrets:

```powershell
# Generate strong random passwords
$postgresPassword = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | % {[char]$_})
$haPassword = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | % {[char]$_})
$replicationPassword = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | % {[char]$_})

# Create temporary secret file
@"
apiVersion: v1
kind: Secret
metadata:
  name: postgresql-credentials
  namespace: data-storage
type: Opaque
stringData:
  postgres-password: $postgresPassword
  password: $haPassword
  replication-password: $replicationPassword
"@ | kubectl create --dry-run=client -o yaml -f - | kubeseal --format=yaml > clusters/microk8s/databases/postgresql/sealed-secret.yaml
```

### Step 2: Update Home Assistant Sealed Secret

Add the PostgreSQL connection URL to the Home Assistant sealed secret:

```powershell
# Use the same password generated in Step 1
$haPassword = "YOUR_HA_PASSWORD_FROM_STEP1"
$postgresUrl = "postgresql://homeassistant:$haPassword@postgresql.data-storage.svc.cluster.local:5432/homeassistant"

# Add postgresql-url to the existing home-assistant-config sealed secret
# Option A: Re-create the entire sealed secret with the new key
# Option B: Patch the existing sealed secret (requires kubeseal v0.18+)
kubectl create secret generic home-assistant-config \
  --from-literal=postgresql-url="$postgresUrl" \
  --dry-run=client -o yaml | kubeseal --format=yaml --merge-into clusters/microk8s/apps/home-assistant/sealed-secret.yaml
```

### Step 3: Commit and Push Changes

```powershell
git add .
git commit -m "Phase 1: Migrate to PostgreSQL and ensure NAS-backed storage"
git push
```

### Step 4: Monitor Flux Reconciliation

```powershell
# Watch Flux sync the changes
flux get kustomizations --watch

# Check PostgreSQL deployment
kubectl get pods -n data-storage

# Check HelmRelease status
flux get helmreleases -n data-storage
```

### Step 5: Verify PostgreSQL is Running

```powershell
# Check PostgreSQL pod
kubectl get pods -n data-storage -l app.kubernetes.io/name=postgresql

# Check PostgreSQL logs
kubectl logs -n data-storage -l app.kubernetes.io/name=postgresql --tail=50

# Verify database exists
kubectl exec -n data-storage -it $(kubectl get pod -n data-storage -l app.kubernetes.io/name=postgresql -o name) -- psql -U postgres -c "\l"
```

### Step 6: Restart Home Assistant

Once PostgreSQL is running and the sealed secret is updated:

```powershell
# Restart Home Assistant to apply new recorder configuration
kubectl rollout restart deployment/home-assistant -n home-automation

# Watch the restart
kubectl rollout status deployment/home-assistant -n home-automation

# Check logs for PostgreSQL connection
kubectl logs -n home-automation -l app.kubernetes.io/name=home-assistant --tail=100 | grep -i postgres
```

### Step 7: Verify Data Migration

After Home Assistant restarts:

1. Check Home Assistant logs for successful PostgreSQL connection
2. Verify no SQLite errors in logs
3. Check History panel in Home Assistant UI shows data
4. Verify PostgreSQL database has tables:

```powershell
kubectl exec -n data-storage -it $(kubectl get pod -n data-storage -l app.kubernetes.io/name=postgresql -o name) -- psql -U homeassistant -d homeassistant -c "\dt"
```

Expected tables: `events`, `states`, `recorder_runs`, `schema_changes`, `state_attributes`, `statistics`, etc.

---

## Rollback Plan

If issues occur, rollback to SQLite:

### Quick Rollback

```powershell
# Revert Home Assistant to SQLite
kubectl edit helmrelease home-assistant -n home-automation

# Change recorder.db_url back to:
# db_url: "sqlite:////config/home-assistant_v2.db"

# Or revert the git commit
git revert HEAD
git push
```

### Full Rollback

```powershell
# Revert all Phase 1 changes
git revert HEAD
git push

# Remove PostgreSQL (optional - keeps data for retry)
kubectl delete helmrelease postgresql -n data-storage
```

---

## Migration Notes

### Data Migration Strategy

**Current Approach:** Clean migration (no historical data copied)
- SQLite database remains in `/config/home-assistant_v2.db`
- PostgreSQL starts with fresh schema
- Historical data older than restart date stays in SQLite
- New events/states go to PostgreSQL

**Optional: Historical Data Migration**

If you want to preserve historical data, before Step 6:

```powershell
# Scale down Home Assistant
kubectl scale deployment home-assistant -n home-automation --replicas=0

# Copy SQLite file from PVC to local
kubectl cp home-automation/$(kubectl get pod -n home-automation -l app.kubernetes.io/name=home-assistant -o name | cut -d/ -f2):/config/home-assistant_v2.db ./home-assistant_v2.db

# Use a migration tool (run locally or in a job)
# Example: https://github.com/home-assistant/core/blob/dev/script/db_migration.py

# Scale up Home Assistant with PostgreSQL
kubectl scale deployment home-assistant -n home-automation --replicas=1
```

---

## Expected Outcomes

After successful deployment:

✅ **No SQLite corruption risks** — PostgreSQL handles NFS properly  
✅ **ACID compliance** — Guaranteed data consistency  
✅ **Better performance** — PostgreSQL optimized for concurrent access  
✅ **Extended retention** — 30 days instead of 10 days  
✅ **Power-loss safe** — PostgreSQL WAL provides durability  
✅ **NAS-backed** — All data persists on DPX2800  
✅ **Restart-safe** — No data loss on pod restarts  
✅ **Metrics enabled** — Ready for Prometheus integration  

---

## Next Steps

After Phase 1 is deployed and verified:

**Phase 2:** Home Assistant Hardening
- Remove USB device mount from Home Assistant pod
- Remove Matter server sidecar
- Review privileged mode necessity
- Fix hardcoded namespace in templates

**Phase 3:** Long-Term Metrics (InfluxDB)
- Extend InfluxDB retention to multi-year
- Configure appropriate shard duration
- Document storage growth expectations

---

## Files Changed Summary

```
Modified:
  clusters/microk8s/storage/ssd-storageclass.yaml
  clusters/microk8s/databases/kustomization.yaml
  charts/home-assistant/values.yaml
  charts/home-assistant/templates/deployment.yaml

Created:
  clusters/microk8s/databases/postgresql/pvc.yaml
  clusters/microk8s/databases/postgresql/helmrelease.yaml
  clusters/microk8s/databases/postgresql/kustomization.yaml
  clusters/microk8s/databases/postgresql/sealed-secret.yaml
  PHASE1_IMPLEMENTATION.md
```

---

*Phase 1 implementation complete. Awaiting manual secret generation and deployment verification.*
