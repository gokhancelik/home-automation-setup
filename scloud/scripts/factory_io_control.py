#!/usr/bin/env python3
"""
Factory I/O Manual Control
Send commands to start conveyors and activate systems
"""

import time
from pymodbus.client import ModbusTcpClient

# Configuration
FACTORY_IO_HOST = "192.168.68.75"
FACTORY_IO_PORT = 502
SLAVE_ID = 1

def control_factory_io():
    """Manual control of Factory I/O"""
    print("Factory I/O Manual Controller")
    print("="*40)
    
    try:
        # Connect to Factory I/O
        client = ModbusTcpClient(FACTORY_IO_HOST, port=FACTORY_IO_PORT)
        if not client.connect():
            print("❌ Failed to connect to Factory I/O")
            return
            
        print(f"✅ Connected to Factory I/O at {FACTORY_IO_HOST}:{FACTORY_IO_PORT}")
        
        # Common Factory I/O electrical panel control commands
        print("\nActivating electrical panel...")
        
        # First, try common electrical panel addresses
        panel_commands = [
            (0, True, "System Power/Enable"),
            (1, True, "Start Button (Panel)"),
            (2, True, "Emergency Reset"),
            (3, False, "Emergency Stop (release)"),
            (4, True, "Auto Mode"),
            (5, True, "System Ready"),
            (6, True, "Main Power"),
            (7, True, "Control Power"),
            (8, True, "Motor Enable"),
            (9, True, "Conveyor Enable"),
            (10, True, "Process Start"),
            (11, True, "Cycle Start"),
        ]
        
        for coil_addr, value, description in panel_commands:
            try:
                result = client.write_coil(address=coil_addr, value=value)
                if not result.isError():
                    status = "PRESSED" if value else "RELEASED"
                    print(f"✅ Panel Control {coil_addr}: {description} -> {status}")
                else:
                    print(f"❌ Failed to activate {coil_addr}: {description}")
                time.sleep(0.2)  # Slower for panel controls
            except Exception as e:
                print(f"❌ Error with panel control {coil_addr}: {e}")
        
        print("\nActivating individual motors...")
        
        # Then activate individual motor controls
        motor_commands = [
            (12, True, "Main conveyor motor"),
            (13, True, "Feed conveyor motor"), 
            (14, True, "Scale conveyor motor"),
            (15, True, "Sort conveyor motor"),
            (16, False, "Diverter (neutral position)"),
            (17, True, "Parts feeder"),
            (18, True, "Scale enable"),
            (19, True, "Reject gate enable"),
        ]
        
        for coil_addr, value, description in motor_commands:
            try:
                result = client.write_coil(address=coil_addr, value=value)
                if not result.isError():
                    status = "ON" if value else "OFF"
                    print(f"✅ Motor {coil_addr}: {description} -> {status}")
                else:
                    print(f"❌ Failed to write motor {coil_addr}: {description}")
                time.sleep(0.1)
            except Exception as e:
                print(f"❌ Error writing motor {coil_addr}: {e}")
        
        # Additional attempt with different addressing for electrical panels
        print("\nTrying alternative panel addresses...")
        
        alt_panel_commands = [
            (100, True, "Panel Start Button"),
            (101, True, "System Enable"),
            (102, True, "Process Enable"),
            (200, True, "HMI Start Button"),
            (201, True, "Manual Start"),
        ]
        
        for coil_addr, value, description in alt_panel_commands:
            try:
                result = client.write_coil(address=coil_addr, value=value)
                if not result.isError():
                    print(f"✅ Alt Panel {coil_addr}: {description} -> ACTIVATED")
                time.sleep(0.1)
            except:
                pass  # Ignore errors for alternative addresses
        
        # Write to holding registers (analog outputs)
        print("\nSetting parameters...")
        
        parameters = [
            (0, 120, "Conveyor speed (RPM)"),
            (1, 150, "Weight threshold light (grams)"),
            (2, 300, "Weight threshold heavy (grams)"),
            (3, 100, "Feed rate"),
        ]
        
        for reg_addr, value, description in parameters:
            try:
                result = client.write_register(address=reg_addr, value=value)
                if not result.isError():
                    print(f"✅ Register {reg_addr}: {description} = {value}")
                else:
                    print(f"❌ Failed to write register {reg_addr}: {description}")
                time.sleep(0.1)
            except Exception as e:
                print(f"❌ Error writing register {reg_addr}: {e}")
        
        print(f"\n🏭 Electrical panel activation commands sent!")
        print("If the start button still cannot be enabled, try:")
        print("1. Check Factory I/O I/O Points mapping:")
        print("   - Go to I/O → I/O Points in Factory I/O")
        print("   - Look for 'Start Button' or 'Panel Start'")
        print("   - Note the Modbus coil address (e.g., %Q0.1)")
        print("2. Check for interlocks:")
        print("   - Emergency stop must be released")
        print("   - Safety gates must be closed")
        print("   - System power must be on")
        print("3. Check scene prerequisites:")
        print("   - Some scenes need parts in feeders first")
        print("   - Others need manual mode selection")
        print("4. Try manual click in Factory I/O:")
        print("   - Right-click the start button → 'Force ON'")
        print("   - This bypasses Modbus and directly activates")
        
        print(f"\n💡 Pro tip: Check the Factory I/O I/O Points window")
        print("   to see the exact coil addresses for your scene!")
        
        client.close()
        
    except Exception as e:
        print(f"❌ Error: {e}")
        print(f"\n🔧 Make sure Factory I/O is running with:")
        print("   - Modbus TCP Slave driver connected")
        print("   - Scene loaded and playing")
        print("   - IP: {FACTORY_IO_HOST}:{FACTORY_IO_PORT}")

if __name__ == "__main__":
    control_factory_io()
