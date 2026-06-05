# Zigbee2MQTT Frontend Access

## Status
✅ **Accessible at: https://zigbee.gcelik.dev**

## What Was Fixed
The Zigbee2MQTT frontend (port 8080) was running on Docker (pi-lab) but not accessible externally because:
1. It was only listening on localhost
2. The old k8s ingress was pointing to the now-defunct k8s service

## Solution
Created an external service endpoint in Kubernetes that routes traffic from the ingress to the Docker container on pi-lab:

### Architecture
```
Internet (zigbee.gcelik.dev)
    ↓ HTTPS (Let's Encrypt TLS)
nginx-ingress (ubuntu-desktop)
    ↓
Service: zigbee2mqtt-frontend (ClusterIP: None)
    ↓
EndpointSlice → 192.168.2.59:8080
    ↓
Docker: zigbee2mqtt container (pi-lab)
```

## Files Changed
- **Created**: `external-service.yaml` - Service + EndpointSlice pointing to 192.168.2.59:8080
- **Modified**: `ingress.yaml` - Updated backend service from `zigbee2mqtt` to `zigbee2mqtt-frontend`
- **Modified**: `kustomization.yaml` - Added external-service.yaml to resources

## Access
- **URL**: https://zigbee.gcelik.dev
- **Authentication**: Configured in Zigbee2MQTT config (`secret.yaml`)
  - Token: `!secret frontend_auth_token`

## Functionality
The Zigbee2MQTT frontend provides:
- Device management (pair, unpair, rename)
- Device configuration
- Group management
- Bindings configuration
- OTA updates
- Network map visualization
- Log viewer
- Settings management

## Security
- ✅ HTTPS with Let's Encrypt certificate
- ✅ Authentication required (frontend auth token)
- ✅ TLS certificate auto-renewal
- ✅ Secure external access

## Related Resources
- **Namespace**: `zigbee`
- **Service**: `zigbee2mqtt-frontend`
- **EndpointSlice**: `zigbee2mqtt-frontend` (192.168.2.59:8080)
- **Ingress**: `zigbee2mqtt` (zigbee.gcelik.dev)
- **Certificate**: `zigbee2mqtt-tls` (Ready)
- **Docker Container**: `zigbee2mqtt` on pi-lab

## Verification
```bash
# Test external access
curl -I https://zigbee.gcelik.dev

# Check service
microk8s kubectl get svc -n zigbee zigbee2mqtt-frontend

# Check endpoint
microk8s kubectl get endpointslice -n zigbee zigbee2mqtt-frontend

# Check ingress
microk8s kubectl get ingress -n zigbee

# Check certificate
microk8s kubectl get certificate -n zigbee
```

## Git Commit
Commit: `fb3f6e3` - Add external service for Zigbee2MQTT frontend on Docker
