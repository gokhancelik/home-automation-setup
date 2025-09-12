# Zigbee Setup Guide

## Prerequisites
✅ Sonoff ZBDongle-P USB coordinator connected to the server
✅ ZHA configuration added to Home Assistant
✅ USB device passthrough configured in Kubernetes

## Current Status
- USB Device: `/dev/ttyUSB0` is available in the container
- ZHA integration is configured in `configuration.yaml`
- Home Assistant is ready for ZHA setup

## Setup Steps

### 1. Add ZHA Integration
1. Open Home Assistant: https://ha.gcelik.dev
2. Go to **Settings** → **Devices & Services**
3. Click **+ ADD INTEGRATION**
4. Search for and select **"ZHA (Zigbee Home Automation)"**

### 2. Configure Zigbee Coordinator
When prompted, enter these settings:
- **Serial Device Path**: `/dev/ttyUSB0`
- **Radio Type**: `Silicon Labs` (for Sonoff ZBDongle-P)
- **Port Speed**: `115200` (default)
- **Flow Control**: Leave as default

### 3. Wait for Network Formation
- ZHA will automatically create a Zigbee network
- This may take 1-2 minutes
- The coordinator will start accepting devices

### 4. Add Your Devices

#### For Sonoff Devices:
1. Put the device in pairing mode (usually hold button for 5-10 seconds)
2. In Home Assistant, go to **ZHA** → **Add Device**
3. Click **"Add devices via this device"** next to your coordinator
4. Follow the device-specific pairing instructions

#### For Aqara FP2 Presence Sensor:
1. Press and hold the reset button on the FP2 for 10 seconds
2. The LED should start flashing blue
3. In ZHA, click **"Add devices via this device"**
4. The FP2 should appear within 2 minutes
5. Configure zones and sensitivity through the device options

## Troubleshooting

### If ZHA doesn't appear in integrations:
1. Check that USB device is accessible:
   ```bash
   kubectl exec deployment/home-assistant -n home-automation -- ls -la /dev/ttyUSB*
   ```

### If coordinator fails to initialize:
1. Restart Home Assistant:
   ```bash
   kubectl rollout restart deployment/home-assistant -n home-automation
   ```

### If devices won't pair:
1. Ensure device is in pairing mode
2. Try factory resetting the device
3. Move device closer to coordinator during pairing

## Supported Devices
- ✅ Sonoff sensors and switches (Zigbee 3.0)
- ✅ Aqara FP2 presence sensor
- ✅ Most Zigbee 3.0 and Zigbee HA 1.2 devices
- ✅ IKEA TRÅDFRI bulbs and switches
- ✅ Philips Hue (when not using Hue Bridge)

## Network Information
Once ZHA is set up, you can view your Zigbee network:
- **Settings** → **Devices & Services** → **ZHA** → **Configure**
- **Zigbee Network Visualization** shows device topology
- **Device Interview** logs show pairing details

## Notes
- ZHA uses a local Zigbee coordinator - no cloud dependency
- Devices paired to ZHA cannot be used with other coordinators simultaneously
- The ZBDongle-P supports up to 20-40 direct children devices
- For larger networks, consider Zigbee routers (powered devices)
