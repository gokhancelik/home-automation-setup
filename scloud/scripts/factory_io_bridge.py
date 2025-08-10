"""
Factory I/O to CloudHMI MQTT Bridge
Converts Modbus data from Factory I/O simulation to MQTT messages for CloudHMI platform
"""

import asyncio
import json
import time
import logging
from datetime import datetime
from typing import Dict, Any

try:
    from pymodbus.client.sync import ModbusTcpClient
    from paho.mqtt import client as mqtt_client
    DEPENDENCIES_AVAILABLE = True
except ImportError:
    DEPENDENCIES_AVAILABLE = False
    print("Missing dependencies. Install with: pip install pymodbus paho-mqtt")

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

class FactoryIoMqttBridge:
    """Bridge between Factory I/O Modbus and CloudHMI MQTT"""
    
    def __init__(self, 
                 modbus_host: str = "127.0.0.1", 
                 modbus_port: int = 502,
                 mqtt_host: str = "localhost", 
                 mqtt_port: int = 1883,
                 update_interval: float = 1.0):
        
        self.modbus_host = modbus_host
        self.modbus_port = modbus_port
        self.mqtt_host = mqtt_host
        self.mqtt_port = mqtt_port
        self.update_interval = update_interval
        
        # Modbus client
        self.modbus_client = None
        
        # MQTT client
        self.mqtt_client = mqtt_client.Client()
        self.mqtt_client.on_connect = self._on_mqtt_connect
        self.mqtt_client.on_disconnect = self._on_mqtt_disconnect
        
        # Data mapping configuration
        self.data_mapping = {
            "conveyor_motor": {"address": 0, "type": "coil", "factor": 1},
            "sensor_1": {"address": 1, "type": "input", "factor": 1},
            "sensor_2": {"address": 2, "type": "input", "factor": 1},
            "temperature": {"address": 0, "type": "holding", "factor": 0.1},
            "pressure": {"address": 1, "type": "holding", "factor": 0.01},
            "speed": {"address": 2, "type": "holding", "factor": 1},
            "part_counter": {"address": 3, "type": "holding", "factor": 1},
            "cycle_time": {"address": 4, "type": "holding", "factor": 0.1}
        }
        
        self.running = False
    
    def _on_mqtt_connect(self, client, userdata, flags, rc):
        """MQTT connection callback"""
        if rc == 0:
            logger.info("Connected to MQTT broker")
        else:
            logger.error(f"Failed to connect to MQTT broker: {rc}")
    
    def _on_mqtt_disconnect(self, client, userdata, rc):
        """MQTT disconnection callback"""
        logger.warning("Disconnected from MQTT broker")
    
    def connect(self) -> bool:
        """Connect to both Modbus and MQTT"""
        try:
            # Connect to Modbus
            self.modbus_client = ModbusTcpClient(self.modbus_host, port=self.modbus_port)
            if not self.modbus_client.connect():
                logger.error("Failed to connect to Modbus server")
                return False
            logger.info(f"Connected to Modbus server at {self.modbus_host}:{self.modbus_port}")
            
            # Connect to MQTT
            self.mqtt_client.connect(self.mqtt_host, self.mqtt_port, 60)
            self.mqtt_client.loop_start()
            
            return True
            
        except Exception as e:
            logger.error(f"Connection error: {e}")
            return False
    
    def disconnect(self):
        """Disconnect from both Modbus and MQTT"""
        self.running = False
        
        if self.modbus_client:
            self.modbus_client.close()
            logger.info("Disconnected from Modbus")
        
        self.mqtt_client.loop_stop()
        self.mqtt_client.disconnect()
        logger.info("Disconnected from MQTT")
    
    def read_modbus_data(self) -> Dict[str, Any]:
        """Read data from Factory I/O via Modbus"""
        data = {}
        
        try:
            # Read coils (digital outputs)
            coils = self.modbus_client.read_coils(0, 16)
            if not coils.isError():
                data["conveyor_motor"] = bool(coils.bits[0])
                data["alarm_active"] = bool(coils.bits[1])
            
            # Read discrete inputs (sensors)
            inputs = self.modbus_client.read_discrete_inputs(0, 16)
            if not inputs.isError():
                data["sensor_1"] = bool(inputs.bits[0])
                data["sensor_2"] = bool(inputs.bits[1])
                data["emergency_stop"] = bool(inputs.bits[2])
            
            # Read holding registers (analog values)
            holdings = self.modbus_client.read_holding_registers(0, 10)
            if not holdings.isError():
                data["temperature"] = holdings.registers[0] * 0.1  # Convert to Celsius
                data["pressure"] = holdings.registers[1] * 0.01   # Convert to bar
                data["speed"] = holdings.registers[2]             # RPM
                data["part_counter"] = holdings.registers[3]      # Parts produced
                data["cycle_time"] = holdings.registers[4] * 0.1  # Seconds
                data["vibration"] = holdings.registers[5] * 0.001 # mm/s
            
            return data
            
        except Exception as e:
            logger.error(f"Error reading Modbus data: {e}")
            return {}
    
    def create_mqtt_message(self, modbus_data: Dict[str, Any]) -> Dict[str, Any]:
        """Convert Modbus data to CloudHMI MQTT message format"""
        
        # Calculate derived values
        status = "Running" if modbus_data.get("conveyor_motor", False) else "Stopped"
        if modbus_data.get("emergency_stop", False):
            status = "Emergency_Stop"
        elif modbus_data.get("alarm_active", False):
            status = "Alarm"
        
        # Create CloudHMI-compatible message
        message = {
            "timestamp": datetime.utcnow().isoformat() + "Z",
            "deviceId": "factory-io-simulator",
            "machineId": "production-line-01",
            "status": status,
            "temperature": modbus_data.get("temperature", 20.0),
            "pressure": modbus_data.get("pressure", 1.0),
            "speed": modbus_data.get("speed", 0),
            "vibration": modbus_data.get("vibration", 0.0),
            "partCounter": modbus_data.get("part_counter", 0),
            "cycleTime": modbus_data.get("cycle_time", 0.0),
            "alarms": [
                {
                    "id": "TEMP_HIGH",
                    "severity": "Warning",
                    "active": modbus_data.get("temperature", 0) > 80,
                    "message": "High temperature detected"
                },
                {
                    "id": "EMERGENCY_STOP",
                    "severity": "Critical",
                    "active": modbus_data.get("emergency_stop", False),
                    "message": "Emergency stop activated"
                }
            ],
            "sensors": {
                "positionSensor1": modbus_data.get("sensor_1", False),
                "positionSensor2": modbus_data.get("sensor_2", False),
                "conveyorMotor": modbus_data.get("conveyor_motor", False)
            }
        }
        
        return message
    
    def publish_data(self, data: Dict[str, Any]):
        """Publish data to MQTT"""
        try:
            topic = "plc/data/factory-io"
            payload = json.dumps(data, indent=2)
            
            result = self.mqtt_client.publish(topic, payload)
            
            if result.rc == mqtt_client.MQTT_ERR_SUCCESS:
                logger.info(f"Published data to {topic}")
                logger.debug(f"Data: {data}")
            else:
                logger.error(f"Failed to publish data: {result.rc}")
                
        except Exception as e:
            logger.error(f"Error publishing data: {e}")
    
    async def run(self):
        """Main bridge loop"""
        logger.info("Starting Factory I/O to CloudHMI bridge...")
        
        if not DEPENDENCIES_AVAILABLE:
            logger.error("Required dependencies not installed")
            return
        
        if not self.connect():
            logger.error("Failed to establish connections")
            return
        
        self.running = True
        
        try:
            while self.running:
                # Read data from Factory I/O
                modbus_data = self.read_modbus_data()
                
                if modbus_data:
                    # Convert to CloudHMI format
                    mqtt_message = self.create_mqtt_message(modbus_data)
                    
                    # Publish to MQTT
                    self.publish_data(mqtt_message)
                else:
                    logger.warning("No data received from Factory I/O")
                
                # Wait for next update
                await asyncio.sleep(self.update_interval)
                
        except KeyboardInterrupt:
            logger.info("Bridge stopped by user")
        except Exception as e:
            logger.error(f"Bridge error: {e}")
        finally:
            self.disconnect()

def main():
    """Main entry point"""
    bridge = FactoryIoMqttBridge(
        update_interval=2.0  # Update every 2 seconds
    )
    
    try:
        asyncio.run(bridge.run())
    except KeyboardInterrupt:
        print("\nBridge stopped by user")

if __name__ == "__main__":
    main()
