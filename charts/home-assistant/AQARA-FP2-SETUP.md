# Aqara FP2 Human Presence Sensor Setup Guide

The Aqara FP2 is a mmWave-based human presence sensor that detects presence, movement, and can create zones for different areas. This guide will help you integrate it with Home Assistant.

## Prerequisites

### 1. Zigbee Coordinator Required
The FP2 is a **Zigbee 3.0 device** and requires one of these USB coordinators:

#### Recommended Coordinators:
- **Sonoff ZBDongle-E** (Zigbee 3.0, ~$15) - Most recommended
- **ConBee II** (deCONZ/ZHA compatible, ~$40)
- **Home Assistant SkyConnect** (Thread + Zigbee, ~$30)
- **Texas Instruments CC2652R** dongles

#### Not Recommended:
- **Sonoff ZBDongle-P** (Zigbee 3.0+ firmware issues)
- **Generic CC2531** (too old, limited device support)

### 2. Hardware Connection
Your Zigbee coordinator needs to be connected to the machine running Home Assistant.

**For Kubernetes/MicroK8s Setup:**
- Connect USB dongle to the node running Home Assistant
- May need USB device passthrough to the container

## Setup Steps

### Step 1: Connect Zigbee Coordinator

#### Physical Connection:
```bash
# Check if USB device is detected
lsusb | grep -i zigbee
# or
dmesg | tail -20
```

#### Find Device Path:
```bash
# List USB serial devices
ls -la /dev/ttyUSB* /dev/ttyACM*
# Common paths:
# /dev/ttyUSB0 (Sonoff dongles)
# /dev/ttyACM0 (ConBee II)
```

### Step 2: Add ZHA Integration

1. **Home Assistant UI**: Go to Settings → Devices & Services → Integrations
2. **Add Integration**: Click "+" and search for "ZHA"
3. **Select Coordinator**: Choose your device type
4. **Device Path**: Enter the path (e.g., `/dev/ttyUSB0`)
5. **Advanced Settings**: Use defaults unless you have specific needs

### Step 3: Pair Aqara FP2

#### Reset FP2 (if previously paired):
1. Hold the reset button on FP2 for 5+ seconds
2. LED should flash indicating factory reset

#### Pairing Process:
1. **ZHA**: Go to ZHA integration → "Add Device"
2. **Put FP2 in Pairing Mode**: 
   - Press and hold the reset button for 3 seconds
   - LED should start flashing quickly (blue/white)
3. **Wait for Discovery**: Should appear within 30-60 seconds
4. **Device Interview**: Let Home Assistant discover all entities

### Step 4: Configure FP2 Zones

The FP2 supports **multiple detection zones** and **room mapping**:

#### Using Aqara Home App (Recommended):
1. **Initial Setup**: Use Aqara Home app first to configure zones
2. **Zone Configuration**: Set up rooms, detection areas, sensitivity
3. **After Setup**: Pair with Home Assistant (zones will transfer)

#### Available Entities:
- **Presence**: Overall room presence
- **Zone Presence**: Individual zone presence (if configured)
- **Movement**: Motion detection
- **Approach/Away**: Direction of movement
- **Light Sensor**: Ambient light level
- **Device Temperature**: Internal sensor temperature

## Troubleshooting

### FP2 Not Discovered

#### Check Zigbee Network:
```yaml
# In Home Assistant Developer Tools → Services
service: zha.permit_joining
data:
  duration: 60  # Allow joining for 60 seconds
```

#### ZHA Device Management:
- **Settings** → **Devices & Services** → **ZHA** → **Configure**
- **Network Map**: Check if FP2 appears in topology
- **Device Interview**: Force re-interview if partially discovered

### Limited Functionality

#### Zone Configuration:
- FP2 zones must be configured via **Aqara Home app** first
- Use Aqara app to set up rooms and sensitivity
- Then pair with Home Assistant

#### Firmware Updates:
- Keep FP2 firmware updated via Aqara Home app
- Newer firmware has better ZHA compatibility

### USB Device Access in Kubernetes

#### Device Passthrough:
You may need to modify the deployment to access USB devices:

```yaml
# Add to deployment.yaml
securityContext:
  privileged: true
volumeMounts:
- name: usb-devices
  mountPath: /dev
volumes:
- name: usb-devices
  hostPath:
    path: /dev
```

#### Alternative: Node Selector:
```yaml
nodeSelector:
  zigbee-coordinator: "true"
```

## Advanced Features

### FP2-Specific Automations

#### Presence-Based Lighting:
```yaml
automation:
  - alias: "FP2 Living Room Lights"
    trigger:
      platform: state
      entity_id: binary_sensor.fp2_living_room_presence
      to: 'on'
    action:
      service: light.turn_on
      entity_id: light.living_room_lights
```

#### Zone-Based Actions:
```yaml
automation:
  - alias: "FP2 Sofa Zone Activity"
    trigger:
      platform: state
      entity_id: binary_sensor.fp2_sofa_zone_presence
      to: 'on'
    action:
      service: media_player.turn_on
      entity_id: media_player.tv
```

### Integration with Other Systems

#### MQTT Bridge (Optional):
- Can bridge FP2 data to MQTT for other systems
- Useful for Node-RED or other automation platforms

#### InfluxDB Logging:
- Your setup already includes InfluxDB
- FP2 sensors will automatically be logged for analytics

## Device Specifications

- **Detection Range**: 5m radius
- **Zones**: Up to 30 configurable zones
- **Detection Height**: 0.5-2.8m
- **Power**: USB-C (5V/1A) or battery
- **Connectivity**: Zigbee 3.0
- **Mounting**: Ceiling mount recommended

## Common Issues & Solutions

### Issue: "Device offline" in Home Assistant
**Solution**: Check USB coordinator connection and ZHA network health

### Issue: "Zones not appearing"
**Solution**: Configure zones in Aqara Home app first, then re-pair

### Issue: "Interference/false detections"
**Solution**: Adjust sensitivity in Aqara app, check for interference sources

### Issue: "USB device not accessible in container"
**Solution**: Add device passthrough or use node selector for coordinator access

The FP2 is one of the most sophisticated presence sensors available and works excellently with Home Assistant once properly configured!
