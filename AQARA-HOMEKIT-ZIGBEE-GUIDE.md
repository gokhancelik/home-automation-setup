# Aqara FP2 with Zigbee and HomeKit Integration

## Overview

Yes, you can absolutely use the **Aqara FP2 presence sensor** with **HomeKit** even though it requires **Zigbee**! Here's how it works:

```
Aqara FP2 → [Zigbee 3.0] → ZHA → Home Assistant → HomeKit Bridge → Apple HomeKit
```

## Technical Details

### 1. Aqara FP2 Connectivity
- **Protocol**: Zigbee 3.0 (NOT WiFi or HomeKit native)
- **Requirement**: Needs a Zigbee coordinator (your Sonoff ZBDongle-P)
- **Connection**: Direct Zigbee mesh network to coordinator

### 2. Home Assistant Integration
- **ZHA Integration**: Connects Zigbee devices to Home Assistant
- **HomeKit Bridge**: Exposes Home Assistant entities to Apple HomeKit
- **Benefits**: Local control, no cloud dependency, full automation capabilities

### 3. HomeKit Functionality
Once connected through ZHA → HomeKit Bridge, the Aqara FP2 will appear in Apple Home as:
- **Motion Sensor**: Presence detection
- **Custom Zones**: Multiple detection areas (configured in HA)
- **Automations**: Works with HomeKit scenes and automations

## Configuration Added

I've configured both integrations for you:

### ZHA (Zigbee Home Automation)
```yaml
zha:
  # Configuration will be done through the UI after adding the integration
  # Supported coordinators: ConBee II, Sonoff ZBDongle-P, SkyConnect, etc.
```

### HomeKit Bridge
```yaml
homekit:
  - name: "Home Assistant Bridge"
    port: 21063
    filter:
      include_domains:
        - light
        - switch
        - sensor
        - binary_sensor
        - climate
        - cover
        - fan
        - lock
        - media_player
      exclude_entities:
        # Exclude noisy sensors that shouldn't be in HomeKit
        - sensor.uptime
        - sensor.date
        - sensor.time
    entity_config:
      # Configure specific devices for HomeKit if needed
      binary_sensor.aqara_fp2_presence:
        name: "Room Presence"
        type: "motion"
```

## Setup Process

### Step 1: Deploy Updated Configuration
The configuration has been updated with:
- ✅ ZHA integration for Zigbee
- ✅ HomeKit Bridge for Apple HomeKit
- ✅ USB device access for Zigbee coordinator
- ✅ Privileged container for hardware access
- ✅ Matter Server container (for UI-based Matter setup)
- ✅ Removed Matter YAML configs (must be configured via UI)

### Step 2: Add ZHA Integration (via UI)
1. Open Home Assistant: https://ha.gcelik.dev
2. Go to **Settings** → **Devices & Services**
3. Click **+ ADD INTEGRATION**
4. Search for "ZHA" and select it
5. Configure with:
   - Serial Device Path: `/dev/ttyUSB0`
   - Radio Type: `Silicon Labs` (for Sonoff ZBDongle-P)

### Step 3: Pair Aqara FP2
1. In ZHA, click **"Add Device"**
2. Put FP2 in pairing mode:
   - Press and hold reset button for 10 seconds
   - LED should flash blue
3. Device will appear in ZHA within 2 minutes
4. Configure zones and sensitivity in device options

### Step 4: HomeKit Bridge Setup
1. Go to **Settings** → **Devices & Services**
2. HomeKit Bridge should auto-discover
3. Click **"Configure"** on the HomeKit integration
4. Note the HomeKit pairing code
5. Open Apple Home app on iPhone/iPad
6. Add accessory and scan the QR code or enter pairing code

## HomeKit Capabilities

Once set up, you'll have:

### In Apple Home App:
- **Room Presence**: Motion sensor for each zone
- **Automations**: Trigger HomeKit scenes based on presence
- **Status**: Real-time presence detection
- **History**: Presence timeline in Home app

### In Home Assistant:
- **Advanced Zones**: Configure multiple detection areas
- **Sensitivity**: Adjust detection sensitivity per zone
- **Distance**: Monitor distance to detected person
- **Attributes**: Access raw sensor data and attributes

## Advantages of This Setup

1. **Local Control**: Everything runs locally, no cloud dependencies
2. **Best of Both Worlds**: HA's advanced automation + HomeKit's ecosystem
3. **Multiple Interfaces**: Control via HA dashboard, Apple Home, or Siri
4. **Advanced Features**: HA provides more configuration options than native HomeKit devices
5. **Integration**: Works with both HA automations and HomeKit scenes

## Device Support

This setup works with any Zigbee device:
- ✅ **Aqara**: FP2, door sensors, temperature sensors, switches
- ✅ **Sonoff**: Zigbee switches, sensors, bulbs
- ✅ **IKEA TRÅDFRI**: Bulbs, switches, motion sensors
- ✅ **Philips Hue**: Bulbs and sensors (when not using Hue Bridge)
- ✅ **Generic Zigbee 3.0**: Most modern Zigbee devices

## Next Steps

1. **Commit and Deploy**: Push the configuration changes
2. **Wait for Deployment**: FluxCD will apply the new configuration
3. **Add ZHA**: Set up Zigbee integration through HA UI
4. **Pair FP2**: Connect your Aqara presence sensor
5. **Setup HomeKit**: Add HA bridge to Apple Home
6. **Configure Zones**: Set up presence detection areas in HA
7. **Create Automations**: Build scenes in both HA and HomeKit

The setup provides the flexibility of Home Assistant with the convenience of HomeKit integration!
