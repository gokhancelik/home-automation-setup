# Phase 0 — Repository Assessment Findings

**Date:** December 17, 2025  
**Status:** READ ONLY — No changes made

---

## Executive Summary

The repository is a functional Flux GitOps deployment for a Home Assistant smart home platform on MicroK8s. While the foundation is solid, several **critical violations** of the architecture requirements exist, along with anti-patterns that compromise data durability and system stability.

---

## Current Architecture Overview

### Namespaces in Use

| Namespace | Purpose | Status |
|-----------|---------|--------|
| `home-automation` | Home Assistant Core | ✅ Exists |
| `data-storage` | InfluxDB | ✅ Exists |
| `observability` | Loki, Grafana, Promtail | ✅ Exists |
| `ingress-nginx` | Ingress Controller | ✅ Exists |
| `cert-manager` | TLS Certificate Management | ✅ Exists |
| `flux-system` | GitOps Controller | ✅ Exists |
| `zigbee` | Zigbee2MQTT (required) | ❌ Missing |

### StorageClasses

| StorageClass | Provisioner | Reclaim Policy | Default | Notes |
|--------------|-------------|----------------|---------|-------|
| `ssd-storage` | microk8s.io/hostpath | Delete | Yes (conflict) | Local SSD at `/mnt/ssd-data` |
| `nfs-storage` | microk8s.io/hostpath | Retain | Yes (conflict) | NAS mount at `/mnt/nas-kubernetes-storage` |

⚠️ **Issue:** Both StorageClasses are marked as default, which causes conflicts.

### PersistentVolumeClaims

| PVC | Namespace | StorageClass | Size | Purpose |
|-----|-----------|--------------|------|---------|
| `home-assistant-data` | home-automation | `nfs-storage` | 5Gi | HA config |
| `influxdb-influxdb2` | data-storage | `nfs-storage` | 20Gi | InfluxDB data |
| `loki-storage` | observability | `nfs-storage` | 10Gi | Loki logs |
| `grafana-storage` | observability | `nfs-storage` | 2Gi | Grafana dashboards |

---

## Critical Violations (Must Fix)

### 🔴 V1: SQLite Still in Use

**Location:** [charts/home-assistant/values.yaml](charts/home-assistant/values.yaml#L72-L73)

```yaml
recorder:
  db_url: "sqlite:////config/home-assistant_v2.db"
```

**Violation:** Architecture explicitly states "NO SQLite" and "SQLite must NOT be used."

**Impact:** SQLite on network storage (NFS) is unreliable and can cause database corruption. PostgreSQL is required for the recorder.

**Required Fix:** Migrate to PostgreSQL in Phase 1.

---

### 🔴 V2: PostgreSQL Not Deployed

**Location:** `clusters/microk8s/databases/postgresql/` — **Folder is empty**

**Violation:** PostgreSQL is required for Home Assistant recorder/history, but no PostgreSQL deployment exists.

**Impact:** Cannot comply with data durability requirements without PostgreSQL.

**Required Fix:** Deploy PostgreSQL with NAS-backed PVC in Phase 1.

---

### 🔴 V3: Matter Server in Same Pod as Home Assistant

**Location:** [charts/home-assistant/templates/deployment.yaml](charts/home-assistant/templates/deployment.yaml#L159-L179)

```yaml
- name: matter-server
  image: ghcr.io/home-assistant-libs/python-matter-server:stable
```

**Violation:** Architecture states Matter must "run isolated in its own pod" and "must not affect HA stability if it crashes."

**Impact:** Matter crash could bring down entire Home Assistant pod.

**Required Fix:** Move Matter server to separate Deployment in Phase 5.

---

### 🔴 V4: USB Device Mounted to Home Assistant Pod

**Location:** [charts/home-assistant/templates/deployment.yaml](charts/home-assistant/templates/deployment.yaml#L148-L150)

```yaml
- name: usb-devices
  mountPath: /dev/ttyUSB0
```

**Violation:** Architecture states "USB Zigbee coordinator mounted only to Zigbee2MQTT pod" and "Home Assistant communicates with Zigbee only via MQTT."

**Impact:** Direct USB access in HA pod prevents proper Zigbee isolation and blocks Zigbee2MQTT deployment.

**Required Fix:** Remove USB mount from HA, deploy Zigbee2MQTT with USB device in Phase 5.

---

### 🔴 V5: No Zigbee2MQTT or MQTT Broker

**Missing Components:**
- No `zigbee` namespace
- No Zigbee2MQTT deployment
- No Mosquitto MQTT broker

**Violation:** Architecture requires Zigbee2MQTT in its own namespace with MQTT broker.

**Required Fix:** Deploy MQTT broker and Zigbee2MQTT in Phase 5.

---

### 🔴 V6: InfluxDB Retention Policy Too Short

**Location:** [clusters/microk8s/databases/influxdb/helmrelease.yaml](clusters/microk8s/databases/influxdb/helmrelease.yaml#L35)

```yaml
retention_policy: "30d"
```

**Violation:** Architecture states "Retain metrics for multiple years" and "Avoid aggressive downsampling by default."

**Impact:** Metrics are deleted after 30 days, losing years of valuable data.

**Required Fix:** Configure multi-year retention in Phase 3.

---

## Anti-Patterns & Issues

### 🟠 A1: Duplicate Default StorageClass

**Files:**
- [clusters/microk8s/storage/ssd-storageclass.yaml](clusters/microk8s/storage/ssd-storageclass.yaml#L6)
- [clusters/microk8s/infrastructure/storage/nfs-provisioner.yaml](clusters/microk8s/infrastructure/storage/nfs-provisioner.yaml#L6)

Both have `storageclass.kubernetes.io/is-default-class: "true"`.

**Impact:** Unpredictable storage class selection for PVCs without explicit storageClassName.

**Fix:** Remove default annotation from `ssd-storage`.

---

### 🟠 A2: Privileged Container

**Location:** [charts/home-assistant/templates/deployment.yaml](charts/home-assistant/templates/deployment.yaml#L137)

```yaml
securityContext:
  privileged: true
```

**Impact:** Security risk. Privileged mode should be avoided unless absolutely necessary.

**Note:** May be required for USB/Bluetooth access. Review when Zigbee is moved to dedicated pod.

---

### 🟠 A3: Hardcoded Grafana Admin Password

**Location:** [clusters/microk8s/infrastructure/observability/grafana-deployment.yaml](clusters/microk8s/infrastructure/observability/grafana-deployment.yaml#L43-L44)

```yaml
- name: GF_SECURITY_ADMIN_PASSWORD
  value: admin
```

**Impact:** Security vulnerability. Credentials should use Sealed Secrets.

**Fix:** Create sealed secret for Grafana credentials.

---

### 🟠 A4: hostNetwork Enabled for Home Assistant

**Location:** [charts/home-assistant/values.yaml](charts/home-assistant/values.yaml#L7-L8)

```yaml
networking:
  hostNetwork: true
```

**Impact:** May cause port conflicts and reduces network isolation.

**Note:** May be required for discovery protocols. Review necessity.

---

### 🟠 A5: Hardcoded Namespace in Deployment Template

**Location:** [charts/home-assistant/templates/deployment.yaml](charts/home-assistant/templates/deployment.yaml#L4)

```yaml
namespace: home-automation
```

**Impact:** Reduces Helm chart reusability. Should use `{{ .Release.Namespace }}`.

---

### 🟠 A6: No Resource Limits on Loki

**Location:** [clusters/microk8s/infrastructure/observability/loki-deployment.yaml](clusters/microk8s/infrastructure/observability/loki-deployment.yaml#L45-L50)

Resources are defined but limits are relatively low (512Mi memory). On a Raspberry Pi with 8GB RAM, this may need tuning.

---

### 🟠 A7: Bluetooth Mounted but Not Used in Architecture

**Location:** [charts/home-assistant/templates/deployment.yaml](charts/home-assistant/templates/deployment.yaml#L152-L156)

```yaml
- name: bluetooth-socket
  mountPath: /var/run/dbus
- name: bluetooth-sys
  mountPath: /sys/class/bluetooth
```

**Impact:** Bluetooth mounts add complexity. Purpose unclear in current architecture.

---

### 🟠 A8: Missing Node Selector on Loki

**Location:** [clusters/microk8s/infrastructure/observability/loki-deployment.yaml](clusters/microk8s/infrastructure/observability/loki-deployment.yaml)

Unlike Grafana and Home Assistant, Loki doesn't have a `nodeSelector`. In a single-node cluster this works, but is inconsistent.

---

## What's Working Well ✅

1. **Flux GitOps Structure:** Proper separation of concerns with sources, infrastructure, databases, and apps layers.

2. **NAS-Backed Storage:** Most stateful services use `nfs-storage` with `Retain` reclaim policy.

3. **Sealed Secrets:** Proper secrets management with Sealed Secrets controller.

4. **Cert-Manager + Let's Encrypt:** Automated TLS certificates with DNS-01 challenge.

5. **Version Pinning:** All Helm charts and images have explicit versions.

6. **Resource Limits:** Home Assistant has proper CPU/memory requests and limits.

7. **Centralized Logging:** Loki + Promtail stack is properly configured.

8. **InfluxDB Integration:** Home Assistant is configured to send metrics to InfluxDB.

9. **Health Probes:** InfluxDB has liveness and readiness probes configured.

10. **Ingress Configuration:** Proper ingress with TLS termination via nginx.

---

## Proposed Changes by Phase

### Phase 1 — Storage & Data Safety

| Priority | Task | Files Affected |
|----------|------|----------------|
| Critical | Deploy PostgreSQL with NAS-backed PVC | `clusters/microk8s/databases/postgresql/*` (new) |
| Critical | Migrate HA recorder from SQLite to PostgreSQL | `charts/home-assistant/values.yaml` |
| High | Fix duplicate default StorageClass | `clusters/microk8s/storage/ssd-storageclass.yaml` |
| High | Add PVC for PostgreSQL | `clusters/microk8s/databases/postgresql/pvc.yaml` (new) |

### Phase 2 — Home Assistant Hardening

| Priority | Task | Files Affected |
|----------|------|----------------|
| High | Remove USB mount from HA pod | `charts/home-assistant/templates/deployment.yaml` |
| High | Remove Matter server sidecar | `charts/home-assistant/templates/deployment.yaml` |
| Medium | Review privileged mode necessity | `charts/home-assistant/templates/deployment.yaml` |
| Medium | Use dynamic namespace in Helm templates | `charts/home-assistant/templates/deployment.yaml` |

### Phase 3 — Long-Term Metrics (InfluxDB)

| Priority | Task | Files Affected |
|----------|------|----------------|
| Critical | Increase retention policy to multi-year | `clusters/microk8s/databases/influxdb/helmrelease.yaml` |
| High | Configure appropriate shard duration for Pi | `clusters/microk8s/databases/influxdb/helmrelease.yaml` |
| Medium | Document expected storage growth | Documentation |

### Phase 4 — Logging & Observability

| Priority | Task | Files Affected |
|----------|------|----------------|
| High | Move Grafana credentials to Sealed Secret | `clusters/microk8s/infrastructure/observability/grafana-deployment.yaml` |
| Medium | Add node selector to Loki | `clusters/microk8s/infrastructure/observability/loki-deployment.yaml` |
| Medium | Configure Grafana dashboards for HA | Dashboard ConfigMaps |

### Phase 5 — Zigbee, Matter & Edge Integrations

| Priority | Task | Files Affected |
|----------|------|----------------|
| Critical | Create `zigbee` namespace | `clusters/microk8s/zigbee/namespace.yaml` (new) |
| Critical | Deploy Mosquitto MQTT broker | `clusters/microk8s/zigbee/mosquitto/*` (new) |
| Critical | Deploy Zigbee2MQTT with USB device | `clusters/microk8s/zigbee/zigbee2mqtt/*` (new) |
| Critical | Move Matter server to isolated pod | `clusters/microk8s/apps/matter-server/*` (new) |
| High | Configure HA to use MQTT for Zigbee | `charts/home-assistant/values.yaml` |

### Phase 6 — Backups & Disaster Recovery

| Priority | Task | Files Affected |
|----------|------|----------------|
| High | Document backup locations | Documentation |
| High | Create restore procedure documentation | Documentation |
| Medium | Consider backup CronJobs for PostgreSQL | `clusters/microk8s/databases/postgresql/backup.yaml` (new) |

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| SQLite corruption on NFS | High | High | Migrate to PostgreSQL immediately |
| Data loss (30-day retention) | Certain | High | Extend InfluxDB retention |
| Matter crash affects HA | Medium | High | Isolate Matter server |
| Zigbee instability | Medium | Medium | Dedicated Zigbee2MQTT namespace |
| Storage class conflict | Medium | Low | Fix default annotation |

---

## Recommendations

1. **Prioritize Phase 1** — The SQLite-on-NFS configuration is the highest risk item.

2. **Create PostgreSQL First** — Deploy and test PostgreSQL before migrating HA recorder.

3. **Plan Zigbee Migration** — The USB device move requires careful planning to avoid Zigbee device re-pairing.

4. **Test Matter Isolation** — Ensure Matter functionality works after isolation before removing from HA pod.

5. **Document Everything** — Each phase should update operational documentation.

---

## Conclusion

The repository has a solid GitOps foundation but contains critical violations that compromise data durability:

- **3 Critical Violations** (SQLite, no PostgreSQL, short retention)
- **3 Architecture Violations** (Matter in HA pod, USB in HA pod, no Zigbee2MQTT)
- **8 Anti-patterns** (security, configuration, consistency issues)

**Recommendation:** Proceed to Phase 1 to address storage and data safety concerns first.

---

*Awaiting approval to proceed to Phase 1.*
