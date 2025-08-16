#!/usr/bin/env python3
"""
Simple Factory I/O to CloudHMI MQTT Bridge
"""

import time
import json
import logging
from datetime import datetime

try:
    import paho.mqtt.client as mqtt_client
    MQTT_AVAILABLE = True
    print("✓ MQTT client imported successfully")
except ImportError as e:
    MQTT_AVAILABLE = False
    print(f"✗ MQTT import failed: {e}")

try:
    from pymodbus.client import ModbusTcpClient
    MODBUS_AVAILABLE = True
    print("✓ Modbus client imported successfully")
except ImportError as e:
    MODBUS_AVAILABLE = False
    print(f"✗ Modbus import failed: {e}")

# Configuration
MODBUS_HOST = "192.168.68.75"  # Your Factory I/O IP address
MODBUS_PORT = 502
MODBUS_SLAVE_ID = 1            # Your Factory I/O Slave ID
MQTT_HOST = "192.168.68.83"
MQTT_PORT = 31883
UPDATE_INTERVAL = 2.0

print(f"\nFactory I/O Bridge Configuration:")
print(f"Modbus: {MODBUS_HOST}:{MODBUS_PORT} (Slave ID: {MODBUS_SLAVE_ID})")
print(f"MQTT: {MQTT_HOST}:{MQTT_PORT}")
print(f"Update Interval: {UPDATE_INTERVAL}s")

def test_mqtt_connection():
    """Test MQTT connection"""
    if not MQTT_AVAILABLE:
        print("✗ MQTT not available - skipping test")
        return False
        
    try:
        client = mqtt_client.Client()
        result = client.connect(MQTT_HOST, MQTT_PORT, 60)
        if result == 0:
            print("✓ MQTT connection successful")
            client.disconnect()
            return True
        else:
            print(f"✗ MQTT connection failed with code: {result}")
            return False
    except Exception as e:
        print(f"✗ MQTT connection error: {e}")
        return False

def test_modbus_connection():
    """Test Modbus connection"""
    if not MODBUS_AVAILABLE:
        print("✗ Modbus not available - skipping test")
        return False
        
    try:
        client = ModbusTcpClient(MODBUS_HOST, port=MODBUS_PORT)
        if client.connect():
            print(f"✓ Modbus connection successful to {MODBUS_HOST}:{MODBUS_PORT} (Slave ID: {MODBUS_SLAVE_ID})")
            
            # Test reading some registers to verify Factory I/O is responding
            try:
                result = client.read_holding_registers(address=0, count=1)
                if not result.isError():
                    print(f"✓ Factory I/O responding - Register 0 value: {result.registers[0]}")
                else:
                    print(f"⚠️  Modbus connected but Factory I/O not responding: {result}")
            except Exception as e:
                print(f"⚠️  Modbus connected but read failed: {e}")
                
            client.close()
            return True
        else:
            print(f"✗ Modbus connection failed to {MODBUS_HOST}:{MODBUS_PORT} - Is Factory I/O running as Modbus TCP Slave?")
            return False
    except Exception as e:
        print(f"✗ Modbus connection error: {e}")
        return False

def read_factory_io_data():
    """Read real data from Factory I/O via Modbus"""
    if not MODBUS_AVAILABLE:
        return None
        
    try:
        client = ModbusTcpClient(MODBUS_HOST, port=MODBUS_PORT)
        if not client.connect():
            return None
            
        # Read typical Factory I/O registers for Sorting by Weight scene
        # These are common addresses - you may need to adjust based on your scene mapping
        
        # Read holding registers (analog values)
        holding_regs = client.read_holding_registers(address=0, count=10)
        
        # Read input registers (sensor values)
        input_regs = client.read_input_registers(address=0, count=10)
        
        # Read coils (digital outputs)
        coils = client.read_coils(address=0, count=16)
        
        # Read discrete inputs (digital sensors)
        discrete_inputs = client.read_discrete_inputs(address=0, count=16)
        
        client.close()
        
        if holding_regs.isError() or input_regs.isError() or coils.isError() or discrete_inputs.isError():
            print("⚠️  Some Modbus reads failed - check Factory I/O I/O mapping")
            return None
            
        # Extract meaningful data from Factory I/O
        # Note: These mappings depend on your specific Factory I/O scene configuration
        factory_data = {
            "timestamp": datetime.utcnow().isoformat() + "Z",
            "device_id": "factory-io",
            "scene": "sorting_by_weight",
            # Weight reading (typically in holding register 0 or 1)
            "weight_reading": holding_regs.registers[0] if len(holding_regs.registers) > 0 else 0,
            "conveyor_speed": holding_regs.registers[1] if len(holding_regs.registers) > 1 else 120,
            "part_counter": holding_regs.registers[2] if len(holding_regs.registers) > 2 else 0,
            # Digital sensors
            "entry_sensor": discrete_inputs.bits[0] if len(discrete_inputs.bits) > 0 else False,
            "weight_sensor": discrete_inputs.bits[1] if len(discrete_inputs.bits) > 1 else False,
            "exit_sensor": discrete_inputs.bits[2] if len(discrete_inputs.bits) > 2 else False,
            # Digital outputs (motors/actuators)
            "conveyor_motor": coils.bits[0] if len(coils.bits) > 0 else False,
            "diverter": coils.bits[1] if len(coils.bits) > 1 else False,
            # Calculated fields
            "temperature": round(25.0 + (holding_regs.registers[3] * 0.1 if len(holding_regs.registers) > 3 else 0), 1),
            "pressure": round(100.0 + (holding_regs.registers[4] * 0.01 if len(holding_regs.registers) > 4 else 0), 1),
            "vibration": round((holding_regs.registers[5] * 0.001 if len(holding_regs.registers) > 5 else 0.05), 3),
            "cycle_count": holding_regs.registers[6] if len(holding_regs.registers) > 6 else 0,
            "is_running": coils.bits[0] if len(coils.bits) > 0 else False,
            "power_consumption": round(2.0 + (holding_regs.registers[7] * 0.1 if len(holding_regs.registers) > 7 else 0), 1),
            "quality": min(100, max(0, holding_regs.registers[8] if len(holding_regs.registers) > 8 else 95)),
            "efficiency": round(min(100, max(0, holding_regs.registers[9] * 0.1 if len(holding_regs.registers) > 9 else 95)), 1)
        }
        
        # Determine part type based on weight
        weight = factory_data["weight_reading"]
        if weight < 100:
            factory_data["part_type"] = "light"
            factory_data["destination"] = "light_bin"
        elif weight < 200:
            factory_data["part_type"] = "medium"
            factory_data["destination"] = "medium_bin"
        else:
            factory_data["part_type"] = "heavy"
            factory_data["destination"] = "heavy_bin"
        
        return factory_data
        
    except Exception as e:
        print(f"✗ Error reading Factory I/O data: {e}")
        return None

def simulate_factory_data():
    """Simulate Factory I/O data since we can't connect to real Factory I/O yet"""
    import random
    
    # Simulate sorting by weight data
    weights = [45, 67, 89, 123, 156, 189, 234, 267, 298, 334]  # Different part weights
    current_weight = random.choice(weights)
    
    # Classification logic
    if current_weight < 100:
        part_type = "light"
        destination = "light_bin"
    elif current_weight < 200:
        part_type = "medium" 
        destination = "medium_bin"
    else:
        part_type = "heavy"
        destination = "heavy_bin"
    
    factory_data = {
        "timestamp": datetime.utcnow().isoformat() + "Z",
        "device_id": "factory-io",
        "scene": "sorting_by_weight",
        "weight_reading": current_weight,
        "part_type": part_type,
        "destination": destination,
        "conveyor_speed": 120,
        "temperature": round(25.0 + random.uniform(-2, 2), 1),
        "pressure": round(100.0 + random.uniform(-5, 5), 1),
        "vibration": round(random.uniform(0.01, 0.1), 3),
        "cycle_count": random.randint(100, 500),
        "is_running": True,
        "power_consumption": round(2.0 + random.uniform(-0.5, 0.5), 1),
        "quality": random.randint(95, 100),
        "efficiency": round(random.uniform(90, 98), 1)
    }
    
    return factory_data

def publish_mqtt_data(data):
    """Publish data to MQTT"""
    if not MQTT_AVAILABLE:
        print(f"[SIMULATED] Would publish to plc/factory-io/data: {json.dumps(data, indent=2)}")
        return
        
    try:
        client = mqtt_client.Client()
        client.connect(MQTT_HOST, MQTT_PORT, 60)
        
        topic = "plc/factory-io/data"
        payload = json.dumps(data)
        
        result = client.publish(topic, payload)
        if result.rc == 0:
            print(f"✓ Published Factory I/O data: Weight={data['weight_reading']}g, Type={data['part_type']}")
        else:
            print(f"✗ Failed to publish: {result.rc}")
            
        client.disconnect()
        
    except Exception as e:
        print(f"✗ MQTT publish error: {e}")

def main():
    """Main bridge function"""
    print("\n" + "="*50)
    print("Factory I/O to CloudHMI Bridge")
    print("="*50)
    
    # Test connections
    print("\nTesting connections...")
    mqtt_ok = test_mqtt_connection()
    modbus_ok = test_modbus_connection()
    
    if not mqtt_ok:
        print("\n⚠️  MQTT connection failed - check if your K8s cluster is running")
        print("   Expected: 192.168.68.83:31883")
        
    if not modbus_ok:
        print("\n⚠️  Modbus connection failed - Factory I/O setup needed:")
        print("   1. Open Factory I/O")
        print("   2. Go to File -> Drivers -> Modbus TCP/IP Slave")
        print("   3. Set IP: 127.0.0.1, Port: 502")
        print("   4. Load 'Sorting by Weight' scene")
        print("   5. Press Play")
    
    print(f"\nStarting bridge loop (will simulate data if no Factory I/O)...")
    print("Press Ctrl+C to stop\n")
    
    try:
        while True:
            # Try to read real Factory I/O data first
            data = read_factory_io_data()
            
            if data:
                # Real Factory I/O data available
                publish_mqtt_data(data)
            else:
                # Fall back to simulated data
                data = simulate_factory_data()
                publish_mqtt_data(data)
            
            # Wait for next cycle
            time.sleep(UPDATE_INTERVAL)
            
    except KeyboardInterrupt:
        print("\n\nBridge stopped by user")
    except Exception as e:
        print(f"\nBridge error: {e}")

if __name__ == "__main__":
    main()
