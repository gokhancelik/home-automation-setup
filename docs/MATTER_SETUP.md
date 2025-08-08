# Home Assistant Matter Support Setup

This document explains the Matter support configuration added to the Home Assistant Kubernetes deployment.

## Changes Made

### 1. Home Assistant Configuration
- **Removed YAML Matter config**: Matter integration should NOT be configured via YAML in modern Home Assistant
- **UI-only configuration**: Matter integration must be added through the Home Assistant web interface
- The networking and security configurations in Kubernetes enable Matter support, but the integration itself is configured via UI

### 2. Kubernetes Configuration Updates

#### Networking
- **Host Network**: Enabled `hostNetwork: true` to allow Home Assistant to access the host's network interfaces
- **DNS Policy**: Set to `ClusterFirstWithHostNet` for proper DNS resolution
- This is required for Matter multicast communication

#### Security Context
- **Privileged**: Enabled to allow access to network interfaces
- **Capabilities**: Added `NET_ADMIN` and `NET_RAW` for network management

#### Ports
- **5540/UDP**: Matter Thread communication
- **5580/UDP**: Matter commissioning
- **8123/TCP**: Home Assistant web interface (existing)

### 3. Deployment Architecture

```
┌─────────────────────┐
│   Ingress (NGINX)   │
│   ha.gcelik.dev     │
└─────────────────────┘
           │
           ▼
┌─────────────────────┐
│     Service         │
│  - 8123/TCP (HTTP) │
│  - 5540/UDP (Matter)│
│  - 5580/UDP (Matter)│
└─────────────────────┘
           │
           ▼
┌─────────────────────┐
│   Home Assistant    │
│   - Host Network    │
│   - Privileged      │
│   - Matter Enabled  │
└─────────────────────┘
```

## Important Notes

### Single Deployment Approach
- **No separate Matter server**: Home Assistant 2025.8.x has native Matter support built-in
- **One container**: Only the `home-assistant` deployment exists - no additional pods needed
- **Integrated solution**: Matter controller functionality is embedded within Home Assistant itself

### How It Works
1. Home Assistant acts as the Matter controller/hub
2. Host networking allows direct access to the network stack for multicast
3. Matter devices communicate directly with Home Assistant
4. No bridge or separate Thread border router container required

## Usage

### Adding Matter Devices

1. **Access Home Assistant**: Navigate to `https://ha.gcelik.dev`

2. **Go to Integrations**: Settings → Devices & Services → Integrations

3. **Add Matter Integration**: 
   - Click "Add Integration"
   - Search for "Matter (BETA)"
   - Click to install the Matter integration
   - **Important**: Do NOT configure Matter via YAML - it's UI-only

4. **Commission Matter Device**:
   - Put your Matter device in pairing mode
   - In Home Assistant, click "Add Device" in the Matter integration
   - Use the Matter QR code or setup code
   - Home Assistant will discover and add the device

### Supported Matter Devices

- Smart Lights (Philips Hue, IKEA, etc.)
- Smart Switches and Outlets
- Door/Window Sensors
- Motion Sensors
- Thermostats
- Smart Locks
- And more...

## Troubleshooting

### 1. Device Not Discovered
- Ensure the device is in pairing mode
- Check that Home Assistant has network access to the device
- Verify the Matter device supports the same network (Wi-Fi/Thread)

### 2. Networking Issues
- Verify host networking is enabled in the deployment
- Check that the security context allows network access
- Ensure UDP ports 5540 and 5580 are accessible

### 3. Checking Logs
```bash
kubectl logs -n home-assistant deployment/home-assistant -f
```

Look for Matter-related log entries to diagnose issues.

## Network Requirements

- **Multicast Support**: Required for Matter device discovery
- **UDP Ports**: 5540 (Thread) and 5580 (commissioning) must be accessible
- **Host Network Access**: Container runs with host networking for full network stack access

## Security Considerations

- The container runs in privileged mode for network access
- Host networking exposes the container to the host's network interfaces
- This is required for Matter to function properly but increases the attack surface
- Ensure your Kubernetes cluster and network are properly secured

## Future Enhancements

- Thread Border Router integration
- Matter Hub functionality for Thread networks
- Enhanced security with non-privileged containers (when Matter support allows)
