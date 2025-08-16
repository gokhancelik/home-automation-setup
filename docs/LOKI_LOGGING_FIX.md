# Loki Logging Fix Summary

## Issues Identified

### 1. Loki Configuration Issues
- **Problem**: Loki was configured for replication (`replication_factor: 3`) but only had 1 instance
- **Symptoms**: "too many unhealthy instances in the ring" errors
- **Solution**: Set `replication_factor: 1` for single instance deployment

### 2. Promtail Configuration Issues
- **Problem**: Read-only filesystem and permission errors
- **Symptoms**: 
  - `read-only file system` errors when writing positions file to `/tmp`
  - `permission denied` errors when accessing `/var/log/syslog`
- **Solutions**:
  - Changed `readOnlyRootFilesystem: false` 
  - Added dedicated positions volume mount to `/tmp/positions`
  - Removed problematic syslog and audit log collection

### 3. Network Connectivity Issues
- **Problem**: Promtail couldn't connect to Loki service
- **Solution**: Updated Promtail to connect via `loki-gateway:80` instead of `loki:3100`

### 4. Log Retention Issues
- **Problem**: Old logs were being rejected due to retention policy
- **Solution**: Extended retention period to 14 days and increased ingestion limits

## Configurations Applied

### Loki HelmRelease Changes
```yaml
loki:
  auth_enabled: false
  commonConfig:
    replication_factor: 1
  limits_config:
    reject_old_samples: true
    reject_old_samples_max_age: 336h  # 14 days
    retention_period: 336h  # 14 days
    ingestion_rate_mb: 8
    ingestion_burst_size_mb: 16
    per_stream_rate_limit: 5MB
    per_stream_rate_limit_burst: 20MB
  storage:
    type: filesystem
    filesystem:
      chunks_directory: /var/loki/chunks
      rules_directory: /var/loki/rules
  ingester:
    lifecycler:
      ring:
        replication_factor: 1
  ruler:
    enable_api: false
```

### Promtail Configuration Changes
```yaml
# Security context changes
securityContext:
  readOnlyRootFilesystem: false  # Changed from true

# Volume mounts
volumeMounts:
  - name: positions
    mountPath: /tmp/positions

# Volumes
volumes:
  - name: positions
    emptyDir: {}

# Client configuration
clients:
  - url: http://loki-gateway:80/loki/api/v1/push
    timeout: 30s
    backoff_config:
      min_period: 500ms
      max_period: 5m
      max_retries: 10

# Positions file
positions:
  filename: /tmp/positions/positions.yaml
```

## Verification Steps

1. **Pod Status**: All logging pods are running and ready
   ```bash
   kubectl get pods -n monitoring
   ```

2. **Log Ingestion**: Loki is actively receiving logs from:
   - Home Assistant pods
   - Matter server
   - Cert-manager
   - PostgreSQL
   - Flux system components

3. **No More Errors**: 
   - No more replication errors in Loki
   - No more filesystem permission errors in Promtail
   - No more connection timeouts

## Current Status

✅ **Fixed**: Loki logging system is now operational
✅ **Ingesting**: Logs from all namespaces and pods
✅ **Accessible**: Via Grafana at `https://grafana.gcelik.dev`

## Access Points

- **Grafana UI**: `https://grafana.gcelik.dev`
- **Loki Gateway**: `loki-gateway.monitoring.svc.cluster.local:80`
- **Direct Loki**: `loki.monitoring.svc.cluster.local:3100`

## Files Modified

1. `clusters/microk8s/infrastructure/logging/loki-helmrelease.yaml`
2. `clusters/microk8s/infrastructure/logging/promtail.yaml`

The logging system is now fully operational and collecting logs from all pods across the cluster.
