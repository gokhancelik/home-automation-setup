# Matter Integration Setup

Matter support has been automatically added to your Home Assistant deployment with a dedicated Matter Server running as a sidecar container.

## What's Included

### Automatic Configuration
- **Matter Server**: Runs as a sidecar container (`ghcr.io/home-assistant-libs/python-matter-server:stable`)
- **WebSocket Connection**: Pre-configured connection to `ws://localhost:5580/ws`
- **Thread Support**: Thread integration enabled for Matter over Thread
- **Persistent Storage**: Matter server data persists in `/data/matter-server`

### Network Ports
- **Home Assistant**: Port 8123 (HTTP)
- **Matter Server**: Port 5580 (WebSocket)

## Setup Steps

### 1. Add Matter Integration
1. Go to **Settings** → **Devices & Services** → **Integrations**
2. Click **+ Add Integration**
3. Search for "Matter (BETA)" and select it
4. Choose **Connect to Matter Server**
5. The URL should auto-fill as `ws://localhost:5580/ws` ✅

### 2. Add Thread Integration (Optional)
1. In **Integrations**, click **+ Add Integration**
2. Search for "Thread" and add it
3. This enables Matter over Thread support

### 3. Commission Matter Devices

#### Option A: Using Home Assistant
1. Go to **Settings** → **Devices & Services** → **Matter**
2. Click **Add Device** → **Add Matter device**
3. Follow the QR code or setup code process

#### Option B: Using Other Controllers
- You can use Apple HomeKit, Google Home, or Samsung SmartThings
- Then share devices with Home Assistant via Matter

## Supported Devices

Matter supports a wide range of device types:
- **Lighting**: Smart bulbs, switches, dimmers
- **Climate**: Thermostats, temperature sensors
- **Security**: Door locks, contact sensors, motion sensors
- **Window Coverings**: Smart blinds, curtains
- **Media**: Smart speakers, displays
- **Sensors**: Air quality, humidity, occupancy

## Troubleshooting

### Matter Server Not Starting
```bash
kubectl logs -n home-automation deployment/home-assistant -c matter-server
```

### Check Matter Integration Status
- Go to **Settings** → **System** → **Repairs**
- Look for any Matter-related issues

### Reset Matter Server Data
If needed, you can reset the Matter server:
```bash
kubectl exec -n home-automation deployment/home-assistant -c matter-server -- rm -rf /data/*
kubectl rollout restart -n home-automation deployment/home-assistant
```

## Thread Border Router

For Thread/Matter over Thread support, you'll need a Thread Border Router:
- **Apple devices**: HomePod mini, Apple TV 4K
- **Google devices**: Nest Hub (2nd gen), Nest Wifi Pro 6E
- **Dedicated**: Nordic nRF52840 dongles with OpenThread

The Thread integration will automatically discover compatible border routers on your network.

## Matter Fabric Management

Home Assistant creates its own Matter fabric. You can:
- **Bridge devices** from other platforms
- **Commission devices** directly to Home Assistant
- **Multi-admin**: Have devices in multiple fabrics (HA + Apple HomeKit, etc.)

Matter enables true interoperability between different smart home platforms!
