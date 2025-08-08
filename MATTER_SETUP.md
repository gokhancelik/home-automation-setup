# Matter Integration Setup Guide

## Overview

Matter support has been successfully deployed with a **separate Matter server architecture** for Home Assistant running in Kubernetes:

- **Home Assistant 2025.8.0b2** - Main automation platform
- **python-matter-server 8.0.0** - Dedicated Matter server service
- **hostNetwork: true** - Required for Matter multicast communication
- **Privileged security context** - Required for network access
- **Chart Version**: 0.4.3 with Matter support and Recreate deployment strategy

## Architecture

The Matter integration uses a **two-pod architecture**:

1. **Home Assistant Pod** - Main application (port 8123)
2. **Matter Server Pod** - Dedicated Matter protocol handler (port 5580)

Both pods run with `hostNetwork: true` to enable proper Matter device communication.

## Important Notes

✅ **Matter server runs as separate service** - Correct architecture approach

⚠️ **Matter server URL required** - Home Assistant connects to external Matter server

❌ **Do NOT access Matter ports directly** - Use Home Assistant UI for device management

## Setup Instructions

### 1. Access Home Assistant

You can access Home Assistant via:

- **Direct**: <http://192.168.68.83:8123> (host network)
- **Ingress**: <https://ha.gcelik.dev> (via NGINX ingress)

### 2. Configure Matter Integration

1. Go to **Settings** → **Devices & Services**
2. Click **"+ Add Integration"**
3. Search for **"Matter"**
4. Click **"Matter (BETA)"**
5. **Enter Matter Server URL**: `ws://192.168.68.83:5580/ws`
6. Complete the setup wizard

### 3. Matter Network Configuration

The following configuration is automatically applied:

```yaml
# Home Assistant deployment
spec:
  template:
    spec:
      hostNetwork: true
      dnsPolicy: ClusterFirstWithHostNet
      securityContext:
        privileged: true
        capabilities:
          add:
            - NET_ADMIN
            - NET_RAW

# Matter server deployment
spec:
  template:
    spec:
      hostNetwork: true
      dnsPolicy: ClusterFirstWithHostNet
      securityContext:
        privileged: true
        capabilities:
          add:
            - NET_ADMIN
            - NET_RAW
      containers:
      - name: matter-server
        image: ghcr.io/home-assistant-libs/python-matter-server:8.0.0
        ports:
        - containerPort: 5580
          name: matter-server
          protocol: TCP
```

### 4. Adding Matter Devices

Once Matter integration is configured:

1. Put your Matter device in pairing mode
2. In Home Assistant, go to **Settings** → **Devices & Services**
3. Find the **Matter** integration
4. Click **"Add Device"** or **"Commission Device"**
5. Follow the commissioning process

### 5. Troubleshooting

#### Matter Integration Setup Fails

- Verify Matter server is running: `kubectl get pods -n home-assistant`
- Check Matter server logs: `kubectl logs matter-server-<pod-id> -n home-assistant`
- Ensure URL is correct: `ws://192.168.68.83:5580/ws`

#### Matter Server Not Starting

- Check persistent volume: `kubectl get pvc -n home-assistant`
- Verify privileged security context is enabled
- Check for port conflicts on the node

#### Device Commission Failures

- Ensure your device supports Matter/Thread
- Check network connectivity between device and Home Assistant
- Verify both pods have hostNetwork enabled
- Reset device and try commissioning again

#### Network Connectivity Tests

```bash
# Test Home Assistant accessibility
curl http://192.168.68.83:8123

# Check Matter server status
kubectl get pods -n home-assistant
kubectl logs matter-server-<pod-name> -n home-assistant

# Verify Matter server port binding
kubectl exec -n home-assistant matter-server-<pod-name> -- ss -tulnp | grep 5580
```

## Deployment Details

- **Home Assistant Chart**: v0.4.3
- **Home Assistant Image**: ghcr.io/home-assistant/home-assistant:2025.8.0b2
- **Matter Server Image**: ghcr.io/home-assistant-libs/python-matter-server:8.0.0
- **Deployment Strategy**: Recreate (prevents port conflicts)
- **Storage**: 5Gi SSD persistent volume (Home Assistant), 1Gi SSD (Matter server)
- **Database**: PostgreSQL with connection pooling

## References

- [Home Assistant Matter Documentation](https://www.home-assistant.io/integrations/matter/)
- [python-matter-server GitHub](https://github.com/home-assistant-libs/python-matter-server)
- [Matter Specification](https://csa-iot.org/all-solutions/matter/)
- [Thread Group Documentation](https://www.threadgroup.org/)
