# Grafana & Loki Setup Guide

## Overview

This setup provides:
- **Loki**: Log aggregation from all Kubernetes pods
- **Promtail**: Log collection agent (DaemonSet on every node)
- **Grafana**: Visualization dashboard accessible at https://grafana.gcelik.dev

## What's Included

### Log Collection
- **Promtail DaemonSet**: Automatically collects logs from all pods
- **Kubernetes Service Discovery**: Automatically discovers new pods
- **Log Parsing**: Extracts metadata (namespace, pod, container, labels)

### Data Sources
- **Loki**: For log data and queries
- **InfluxDB**: For Home Assistant metrics (reusing your existing token)

### Access
- **URL**: https://grafana.gcelik.dev
- **Username**: admin
- **Password**: admin (change this after first login!)

## Deployment Steps

1. **Apply the configuration**:
   ```bash
   kubectl apply -k clusters/microk8s/infrastructure/observability/
   ```

2. **Wait for pods to be ready**:
   ```bash
   kubectl get pods -n observability -w
   ```

3. **Check ingress and certificate**:
   ```bash
   kubectl get ingress -n observability
   kubectl get certificates -n observability
   ```

## Post-Deployment Configuration

### 1. Update InfluxDB Token in Grafana
The Grafana datasource needs your actual InfluxDB token. You'll need to:

1. Get your InfluxDB token:
   ```bash
   kubectl get secret home-assistant-config -n home-automation -o jsonpath='{.data.influxdb-token}' | base64 -d
   ```

2. In Grafana (https://grafana.gcelik.dev):
   - Go to **Configuration** → **Data Sources**
   - Edit the **InfluxDB** datasource
   - Replace `INFLUXDB_TOKEN_PLACEHOLDER` with your actual token
   - Test the connection

### 2. Import Dashboards

You can import these useful dashboards:

#### For Kubernetes Logs:
- **Dashboard ID**: 13639 (Kubernetes Cluster Monitoring via Loki)
- **Dashboard ID**: 15141 (Kubernetes Logs App)

#### For Home Assistant:
- **Dashboard ID**: 12329 (Home Assistant Dashboard)
- Create custom dashboards using your InfluxDB data

### 3. Explore Logs

In Grafana:
1. Go to **Explore**
2. Select **Loki** as data source
3. Try these LogQL queries:

```logql
# All logs from home-automation namespace
{namespace="home-automation"}

# Home Assistant specific logs
{namespace="home-automation", container="home-assistant"}

# Error logs across all pods
{} |= "error" or "ERROR"

# Tesla integration logs
{namespace="home-automation"} |= "tesla"

# ZHA/Zigbee logs
{namespace="home-automation"} |= "zha" or "zigbee"
```

## Useful Log Queries

### Monitor Home Assistant Startup
```logql
{namespace="home-automation", container="home-assistant"} |= "started"
```

### Track Integration Errors
```logql
{namespace="home-automation"} |= "integration" |= "error"
```

### Monitor Certificate Renewal
```logql
{namespace="cert-manager"} |= "certificate" |= "renewed"
```

### Watch Flux Deployments
```logql
{namespace="flux-system"} |= "reconcile"
```

## Architecture

```
Kubernetes Pods → Promtail (DaemonSet) → Loki → Grafana
Home Assistant → InfluxDB → Grafana
```

## Storage Notes

- **Loki**: Uses ephemeral storage (logs are not persistent across restarts)
- **Grafana**: Uses ephemeral storage (dashboards will be lost on restart)
- **InfluxDB**: Your existing persistent storage handles long-term metrics

For production, consider adding persistent volumes for Loki and Grafana.

## Security Notes

- Default Grafana password is `admin/admin` - **change this immediately**
- Consider enabling authentication integration with your existing SSO
- Loki has no authentication by default (secured by Kubernetes RBAC)

## Troubleshooting

### Check Promtail is collecting logs:
```bash
kubectl logs -n observability -l app=promtail
```

### Check Loki is receiving data:
```bash
kubectl logs -n observability -l app=loki
```

### Check Grafana startup:
```bash
kubectl logs -n observability -l app=grafana
```

### Verify log collection:
```bash
# Check if Promtail can reach Loki
kubectl exec -n observability -l app=promtail -- wget -qO- http://loki:3100/ready
```
