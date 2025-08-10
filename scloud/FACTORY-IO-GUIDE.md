# Factory I/O Installation & Setup Guide for CloudHMI

## Quick Start Guide

### 1. Download & Install Factory I/O
1. Visit **https://factoryio.com/**
2. Download Factory I/O (free demo available)
3. Run installer as Administrator
4. Complete installation

### 2. Install Python Dependencies (for bridge)
```powershell
# Install Python packages for MQTT bridge
pip install pymodbus paho-mqtt
```

### 3. Start CloudHMI Infrastructure
```powershell
# Start InfluxDB and MQTT broker
docker-compose up -d

# Verify services are running
docker ps
```

### 4. Test Without Factory I/O (Simulation Mode)
```powershell
# Run test data simulator (no Factory I/O needed)
.\scripts\simple-factory-test.ps1 -DurationMinutes 5

# Start CloudHMI services
dotnet run --project src/SmartCloud.Gateway
dotnet run --project src/SmartCloud.Dashboard

# View dashboard at: http://localhost:5000
```

## Factory I/O Configuration

### Option 1: Modbus TCP Setup (Recommended)
1. Open Factory I/O
2. Go to **File → Drivers → Modbus TCP**
3. Configure:
   - IP Address: `127.0.0.1`
   - Port: `502`
   - Slave ID: `1`
   - Update Rate: `100ms`

### Option 2: OPC UA Setup (Advanced)
1. Go to **File → Drivers → OPC UA**
2. Configure:
   - Server URL: `opc.tcp://localhost:4840`
   - Security Policy: `None`

## Recommended Scenes for Testing

### 1. Sorting by Height
- **Best for**: Basic conveyor and sensor testing
- **Data Points**: Position sensors, motor status, part counters
- **Complexity**: Beginner

### 2. Pick and Place
- **Best for**: Robot automation testing
- **Data Points**: Robot position, gripper status, cycle times
- **Complexity**: Intermediate

### 3. Buffer Station
- **Best for**: Queue management and throughput analysis
- **Data Points**: Buffer levels, throughput rates, efficiency
- **Complexity**: Advanced

## Integration Methods

### Method 1: Direct Simulation (No Factory I/O needed)
```powershell
# Use our test simulator - works immediately
.\scripts\simple-factory-test.ps1
```

### Method 2: Modbus Bridge (Requires Factory I/O)
```powershell
# Install dependencies
.\scripts\factory-io-setup.ps1 -InstallDependencies

# Run bridge (connects Factory I/O to CloudHMI)
python scripts\factory_io_bridge.py
```

### Method 3: MQTT Publisher (Custom integration)
```python
# Example Python code to publish Factory I/O data
import paho.mqtt.client as mqtt
import json

client = mqtt.Client()
client.connect("localhost", 1883, 60)

data = {
    "deviceId": "factory-io",
    "temperature": 25.5,
    "status": "Running"
}

client.publish("plc/data/factory-io", json.dumps(data))
```

## Testing Your Setup

### 1. Verify MQTT Broker
```powershell
# Test MQTT connection
docker run --rm eclipse-mosquitto mosquitto_sub -h localhost -t "plc/data/factory-io" -C 5
```

### 2. Check InfluxDB
- Open: http://localhost:8086
- Username: `admin`
- Password: `password123`
- Organization: `cloudhmi`
- Bucket: `sensor_data`

### 3. Monitor Dashboard
- Open: http://localhost:5000
- Should show real-time data updates
- Check machine status and sensor readings

## Troubleshooting

### Factory I/O Not Starting
- Run as Administrator
- Check Windows Defender/Antivirus
- Verify system requirements (DirectX 11, 8GB RAM)

### MQTT Connection Issues
```powershell
# Check if broker is running
docker ps | grep mosquitto

# Restart broker if needed
docker-compose restart mqtt
```

### No Data in Dashboard
1. Verify MQTT broker is running
2. Check CloudHMI Gateway logs
3. Test with simulation script first
4. Verify topic names match

### Python Dependencies
```powershell
# If pip install fails, try:
python -m pip install --upgrade pip
python -m pip install pymodbus paho-mqtt
```

## Production Deployment

### 1. Scale Testing
- Run multiple Factory I/O instances
- Simulate different production lines
- Test alarm scenarios
- Load test with high-frequency data

### 2. Real PLC Integration
- Replace Factory I/O with actual PLCs
- Use same MQTT topics
- Implement OPC UA for industrial protocols
- Add security certificates

### 3. Cloud Deployment
- Deploy to Azure/AWS
- Use managed MQTT services
- Scale InfluxDB cluster
- Implement authentication

## Resources

- **Factory I/O Documentation**: https://docs.factoryio.com/
- **CloudHMI Dashboard**: http://localhost:5000
- **InfluxDB UI**: http://localhost:8086
- **MQTT Broker**: localhost:1883

---
*This guide helps you simulate industrial processes for testing CloudHMI without needing real factory equipment.*
