# Factory I/O Setup Guide for CloudHMI Platform

## Overview
Factory I/O is a 3D factory simulation software that will provide realistic industrial process data for testing your CloudHMI platform. This guide covers installation, configuration, and integration with your MQTT-based data ingestion system.

## 📥 Factory I/O Installation

### Step 1: Download Factory I/O
1. Visit the official Factory I/O website: https://factoryio.com/
2. Download the latest version (Factory I/O v2.5+)
3. The software offers a free demo with limited scenes

### Step 2: System Requirements
- **OS**: Windows 10/11 (64-bit)
- **RAM**: 8GB minimum, 16GB recommended
- **Graphics**: DirectX 11 compatible
- **Storage**: 4GB available space
- **Processor**: Intel i5 or AMD equivalent

### Step 3: Installation Process
1. Run the Factory I/O installer as Administrator
2. Follow the installation wizard
3. Accept the license agreement
4. Choose installation directory (default recommended)
5. Complete the installation

## 🔌 PLC Integration Options

Factory I/O supports multiple PLC communication methods:

### Option 1: Modbus TCP (Recommended for CloudHMI)
- **Pros**: Easy to implement, widely supported
- **Communication**: Direct TCP/IP connection
- **Data Format**: Standard Modbus registers

### Option 2: OpenPLC Integration
- **Pros**: Open-source, MQTT native support
- **Setup**: Requires OpenPLC runtime installation
- **Benefit**: Direct MQTT publishing capability

### Option 3: OPC UA (Advanced)
- **Pros**: Industrial standard, rich data types
- **Setup**: Requires OPC UA server configuration
- **Use Case**: Production-like testing

## 🚀 Quick Start Configuration

### Step 1: Configure Factory I/O for Modbus TCP
1. Open Factory I/O
2. Go to **File → Drivers → Modbus TCP**
3. Set the following configuration:
   ```
   IP Address: 127.0.0.1 (localhost)
   Port: 502
   Slave ID: 1
   Update Rate: 100ms
   ```

### Step 2: Load a Demo Scene
1. Go to **File → Open Scene**
2. Select a demo scene (e.g., "Sorting by Height")
3. Click **Play** to start simulation

### Step 3: Map I/O Points
1. Open **Configuration → I/O Points**
2. Map sensors and actuators to Modbus addresses:
   ```
   Conveyor Motor: %Q0.0 (Coil 1)
   Sensor 1: %I0.0 (Input 1)
   Sensor 2: %I0.1 (Input 2)
   Counter: %MD0 (Holding Register 1-2)
   ```

## 🔗 CloudHMI Integration

### Step 1: Install Modbus to MQTT Bridge
Create a simple bridge service to convert Modbus data to MQTT:

```powershell
# Install Python and required packages
pip install pymodbus paho-mqtt asyncio
```

### Step 2: Create Modbus-MQTT Bridge Script
```python
# Create modbus_mqtt_bridge.py in your scripts folder
import asyncio
import json
import time
from pymodbus.client.sync import ModbusTcpClient
from paho.mqtt import client as mqtt_client

class ModbusMqttBridge:
    def __init__(self):
        self.modbus_client = ModbusTcpClient('127.0.0.1', port=502)
        self.mqtt_client = mqtt_client.Client()
        self.mqtt_client.connect("localhost", 1883, 60)
        
    def read_and_publish(self):
        if self.modbus_client.connect():
            # Read holding registers
            result = self.modbus_client.read_holding_registers(0, 10)
            if result.isError():
                print("Error reading Modbus data")
                return
                
            # Convert to MQTT message
            data = {
                "timestamp": time.time(),
                "deviceId": "factory-io-sim",
                "machineId": "sorting-line-01",
                "temperature": result.registers[0] / 10.0,
                "pressure": result.registers[1] / 100.0,
                "speed": result.registers[2],
                "status": "Running" if result.registers[3] > 0 else "Stopped"
            }
            
            # Publish to MQTT
            self.mqtt_client.publish("plc/data/factory-io", json.dumps(data))
            print(f"Published: {data}")
            
        self.modbus_client.close()
```

### Step 3: Update CloudHMI MQTT Configuration
Your existing `MqttDataIngestionService` should already handle the `plc/data/factory-io` topic.

## 📊 Recommended Factory I/O Scenes for Testing

### 1. Sorting by Height
- **Purpose**: Basic conveyor and sensor testing
- **Data Points**: Position sensors, motor status, part counters
- **Use Case**: Test basic data ingestion and real-time monitoring

### 2. Pick and Place
- **Purpose**: Complex automation sequence testing
- **Data Points**: Robot position, gripper status, cycle times
- **Use Case**: Test predictive maintenance algorithms

### 3. Buffer Station
- **Purpose**: Queue management and throughput analysis
- **Data Points**: Buffer levels, throughput rates, efficiency metrics
- **Use Case**: Test analytics and reporting features

## 🔧 Advanced Configuration

### OpenPLC Integration (Alternative Approach)
If you prefer direct MQTT publishing without Modbus bridge:

1. **Install OpenPLC Runtime**:
   ```powershell
   # Download from: https://autonomylogic.com/
   # Install OpenPLC Runtime for Windows
   ```

2. **Configure MQTT Publishing**:
   - Add MQTT client library to OpenPLC
   - Configure direct publishing to your CloudHMI topics

3. **Connect Factory I/O to OpenPLC**:
   - Use OpenPLC as intermediate layer
   - Factory I/O → OpenPLC → MQTT → CloudHMI

### Custom Scene Development
1. **Scene Editor**: Use Factory I/O's built-in scene editor
2. **Custom Logic**: Implement specific industrial processes
3. **Data Simulation**: Create realistic sensor noise and variations

## 🚨 Troubleshooting

### Common Issues:

1. **Modbus Connection Failed**
   - Check firewall settings
   - Verify Factory I/O Modbus driver is enabled
   - Ensure no other Modbus clients are connected

2. **MQTT Data Not Appearing**
   - Verify MQTT broker is running: `docker ps`
   - Check topic subscriptions in CloudHMI logs
   - Test MQTT manually: `mosquitto_pub -h localhost -t plc/data/test -m "test"`

3. **Performance Issues**
   - Reduce Factory I/O update rate (500ms instead of 100ms)
   - Optimize MQTT message frequency
   - Monitor CPU and memory usage

## 📈 Testing Your CloudHMI Platform

### Step 1: Start Infrastructure
```powershell
# Start InfluxDB and MQTT broker
docker-compose up -d
```

### Step 2: Launch CloudHMI Services
```powershell
# Terminal 1: Start Gateway
dotnet run --project src/SmartCloud.Gateway

# Terminal 2: Start Dashboard
dotnet run --project src/SmartCloud.Dashboard
```

### Step 3: Start Factory I/O Simulation
1. Open Factory I/O
2. Load your configured scene
3. Start the simulation
4. Run the Modbus-MQTT bridge script

### Step 4: Verify Data Flow
1. Check CloudHMI Dashboard at `http://localhost:5000`
2. Monitor real-time data updates
3. Verify data storage in InfluxDB
4. Test predictive maintenance alerts

## 🎯 Next Steps

1. **Create Custom Scenes**: Design scenes specific to your industry
2. **Implement Alarm Logic**: Configure alarms based on Factory I/O data
3. **Historical Analysis**: Use Factory I/O to generate weeks of historical data
4. **Load Testing**: Simulate multiple production lines simultaneously
5. **Integration Testing**: Test all CloudHMI features end-to-end

## 📚 Resources

- **Factory I/O Documentation**: https://docs.factoryio.com/
- **Modbus Protocol**: https://modbus.org/
- **OpenPLC**: https://autonomylogic.com/
- **CloudHMI Project**: Your current workspace

---

**Note**: Factory I/O demo version has limitations. For full testing capabilities, consider purchasing a license for unlimited scenes and advanced features.
