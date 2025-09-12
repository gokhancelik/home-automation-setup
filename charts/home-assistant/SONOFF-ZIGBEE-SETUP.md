# Sonoff Zigbee Device Setup Guide

This guide covers setting up Sonoff Zigbee devices with Home Assistant, including both USB coordinators and end devices.

## 🔌 Sonoff Zigbee Device Types

### USB Coordinators (Gateway Devices)
- **Sonoff ZBDongle-E** - Recommended (Zigbee 3.0, Silicon Labs EFR32MG21)
- **Sonoff ZBDongle-P** - TI CC2652P (requires firmware update for best compatibility)

### End Devices (Need Coordinator)
- **SNZB-01** - Wireless Switch
- **SNZB-02** - Temperature & Humidity Sensor  
- **SNZB-03** - Motion Sensor
- **SNZB-04** - Door/Window Sensor
- **BASICZBR3** - Smart Switch (relay)
- **S26R2ZB** - Smart Plug
- **And many more...**

## 🛠️ Setup Instructions

### Step 1: Identify Your Device Type

#### If you have a USB Coordinator (ZBDongle):
1. **Physical Connection**: Connect to your Kubernetes node
2. **Device Path**: Usually `/dev/ttyUSB0` or `/dev/ttyACM0`
3. **Check Detection**: 
   ```bash
   # On your Kubernetes node
   lsusb | grep -i silicon
   # or
   dmesg | tail -10
   ```

#### If you have End Devices Only:
You'll need a Zigbee coordinator first. Options:
- Buy a Sonoff ZBDongle-E (~$15)
- Use another compatible coordinator
- Set up Zigbee2MQTT with existing gateway

### Step 2: USB Device Access (For Coordinators)

Your Home Assistant deployment has been updated with USB device access:

```yaml
# Added to deployment:
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

### Step 3: Add ZHA Integration

1. **Home Assistant**: Go to Settings → Devices & Services → Integrations
2. **Add Integration**: Click "+" and search for "ZHA"
3. **Radio Type**: Choose based on your coordinator:
   - **ZBDongle-E**: Select "Silicon Labs EZSP"
   - **ZBDongle-P**: Select "ZNP (Texas Instruments)"
4. **Device Path**: 
   - Try `/dev/ttyUSB0` first
   - If not found, try `/dev/ttyUSB1`, `/dev/ttyACM0`, etc.
5. **Advanced Settings**: Use defaults unless you know what you're changing

### Step 4: Pair Sonoff Devices

#### General Pairing Process:
1. **ZHA**: Go to ZHA integration → "Add Device"
2. **Device Pairing Mode**: Each device has different methods:

#### Device-Specific Pairing:

**SNZB-01 (Wireless Switch):**
- Hold button for 5+ seconds until LED flashes quickly

**SNZB-02 (Temp/Humidity):**
- Press button 3 times quickly, LED should flash

**SNZB-03 (Motion Sensor):**
- Press button 3 times quickly within 2 seconds

**SNZB-04 (Door/Window):**
- Press button 3 times quickly, LED flashes blue

**BASICZBR3 (Smart Switch):**
- Hold physical button for 5+ seconds
- Or toggle power 5 times quickly

**S26R2ZB (Smart Plug):**
- Hold button for 5+ seconds until LED blinks rapidly

### Step 5: Verify Integration

1. **Check Devices**: Settings → Devices & Services → ZHA → Devices
2. **Entity Names**: Devices should appear with proper names
3. **Test Functionality**: Try controlling switches, check sensor readings

## 🚨 Troubleshooting

### USB Coordinator Not Detected

#### Check Physical Connection:
```bash
# SSH into your Kubernetes node
kubectl exec -it deployment/home-assistant -n home-automation -- /bin/bash

# Inside container, check USB devices
ls -la /dev/ttyUSB* /dev/ttyACM*
```

#### Common Device Paths:
- **ZBDongle-E**: `/dev/ttyUSB0` (Silicon Labs)
- **ZBDongle-P**: `/dev/ttyUSB0` (TI CC2652P)
- **ConBee**: `/dev/ttyACM0`

#### Firmware Issues (ZBDongle-P):
If using ZBDongle-P, you may need to flash updated firmware:
- Use official Sonoff firmware or Z2M firmware
- Flash via TI Flash Programmer or cc2538-bsl

### Device Pairing Issues

#### Reset Device:
- Each device has specific reset procedures
- Usually involves holding button for 5+ seconds
- LED patterns indicate pairing mode

#### Network Issues:
```yaml
# Force permit joining via Developer Tools
service: zha.permit_joining
data:
  duration: 60
```

#### Check Zigbee Network:
- **ZHA Network Map**: Settings → ZHA → Configure → Visualize
- **Signal Strength**: Ensure devices are within range
- **Interference**: Check for WiFi interference (use different channels)

### Container Permissions

#### If USB access fails:
```yaml
# May need to add to deployment
securityContext:
  privileged: true
  capabilities:
    add:
    - SYS_ADMIN
```

#### Node Selector (Alternative):
```yaml
# Pin to specific node with USB coordinator
nodeSelector:
  zigbee.coordinator: "true"
```

## 📊 Device Capabilities

### Sonoff Device Features in Home Assistant:

**SNZB-01 (Switch):**
- Button press events
- Battery level
- Device temperature

**SNZB-02 (Temp/Humidity):**
- Temperature sensor
- Humidity sensor  
- Battery level

**SNZB-03 (Motion):**
- Motion detection
- Illuminance sensor
- Battery level

**SNZB-04 (Door/Window):**
- Contact sensor (open/closed)
- Battery level

**BASICZBR3 (Switch):**
- On/off control
- Power monitoring (if supported)
- Status LED control

**S26R2ZB (Plug):**
- On/off control
- Power monitoring
- Status LED

## 🔧 Advanced Configuration

### Custom Device Names:
```yaml
# In Home Assistant
# Settings → Devices → [Your Device] → Settings icon
# Change "Device name" and "Area"
```

### Automation Examples:

#### Motion-Activated Lighting:
```yaml
automation:
  - alias: "Sonoff Motion Light"
    trigger:
      platform: state
      entity_id: binary_sensor.snzb_03_motion
      to: 'on'
    action:
      service: switch.turn_on
      entity_id: switch.basiczbr3_switch
```

#### Temperature Alert:
```yaml
automation:
  - alias: "High Temperature Alert"
    trigger:
      platform: numeric_state
      entity_id: sensor.snzb_02_temperature
      above: 25
    action:
      service: notify.mobile_app
      data:
        message: "Temperature is {{ states('sensor.snzb_02_temperature') }}°C"
```

## 🎛️ Alternative: Zigbee2MQTT

If ZHA doesn't work well, consider Zigbee2MQTT:
- More device support
- Advanced features
- MQTT integration
- Web-based configuration

## 🔄 Updates & Maintenance

### Firmware Updates:
- Sonoff devices usually auto-update via coordinator
- Check device firmware in ZHA device info
- Some devices support OTA updates

### Coordinator Firmware:
- Keep ZBDongle firmware updated
- Check Sonoff or community firmware releases
- Backup Zigbee network before updates

Sonoff devices are generally very reliable with Home Assistant once properly paired!
