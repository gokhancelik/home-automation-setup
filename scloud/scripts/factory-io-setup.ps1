# Factory I/O Setup and Bridge Launcher for CloudHMI
# This script helps install dependencies and launch the Factory I/O to MQTT bridge

param(
    [switch]$InstallDependencies,
    [switch]$RunBridge,
    [switch]$TestMqtt,
    [string]$FactoryIoPath = "C:\Program Files\Factory IO"
)

Write-Host "CloudHMI Factory I/O Integration Setup" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Green

function Test-PythonInstallation {
    try {
        $pythonVersion = python --version 2>&1
        if ($pythonVersion -match "Python (\d+\.\d+)") {
            Write-Host "✅ Python found: $pythonVersion" -ForegroundColor Green
            return $true
        }
    } catch {
        Write-Host "❌ Python not found. Please install Python 3.8+ from https://python.org" -ForegroundColor Red
        return $false
    }
}

function Install-PythonDependencies {
    Write-Host "`n📦 Installing Python dependencies..." -ForegroundColor Yellow
    
    $packages = @(
        "pymodbus==3.5.2",
        "paho-mqtt==1.6.1",
        "asyncio"
    )
    
    foreach ($package in $packages) {
        Write-Host "Installing $package..." -ForegroundColor Cyan
        try {
            pip install $package
            Write-Host "✅ $package installed successfully" -ForegroundColor Green
        } catch {
            Write-Host "❌ Failed to install $package" -ForegroundColor Red
        }
    }
}

function Test-FactoryIoInstallation {
    if (Test-Path $FactoryIoPath) {
        Write-Host "✅ Factory I/O found at: $FactoryIoPath" -ForegroundColor Green
        return $true
    } else {
        Write-Host "❌ Factory I/O not found at: $FactoryIoPath" -ForegroundColor Red
        Write-Host "   Please install Factory I/O from: https://factoryio.com/" -ForegroundColor Yellow
        return $false
    }
}

function Start-Infrastructure {
    Write-Host "`n🐳 Starting CloudHMI infrastructure..." -ForegroundColor Yellow
    
    # Check if Docker is running
    try {
        docker --version | Out-Null
        Write-Host "✅ Docker is available" -ForegroundColor Green
    } catch {
        Write-Host "❌ Docker not found. Please install Docker Desktop" -ForegroundColor Red
        return $false
    }
    
    # Start infrastructure services
    if (Test-Path "docker-compose.yml") {
        Write-Host "Starting InfluxDB and MQTT broker..." -ForegroundColor Cyan
        docker-compose up -d
        
        # Wait for services to start
        Write-Host "Waiting for services to start..." -ForegroundColor Cyan
        Start-Sleep -Seconds 10
        
        # Test MQTT broker
        Write-Host "Testing MQTT broker connection..." -ForegroundColor Cyan
        docker run --rm --network cloudhmi-network eclipse-mosquitto mosquitto_pub -h cloudhmi-mqtt -t "test/connection" -m "bridge-test"
        
        Write-Host "✅ Infrastructure started successfully" -ForegroundColor Green
        return $true
    } else {
        Write-Host "❌ docker-compose.yml not found" -ForegroundColor Red
        return $false
    }
}

function Test-MqttConnection {
    Write-Host "`n📡 Testing MQTT connection..." -ForegroundColor Yellow
    
    # Create a simple test script
    $testScript = @"
import paho.mqtt.client as mqtt
import json
import time

def on_connect(client, userdata, flags, rc):
    if rc == 0:
        print("✅ Connected to MQTT broker successfully")
        client.subscribe("plc/data/factory-io")
    else:
        print(f"❌ Failed to connect to MQTT broker: {rc}")

def on_message(client, userdata, msg):
    print(f"📨 Received message on {msg.topic}")
    try:
        data = json.loads(msg.payload.decode())
        print(f"   Device: {data.get('deviceId', 'Unknown')}")
        print(f"   Status: {data.get('status', 'Unknown')}")
        print(f"   Temperature: {data.get('temperature', 0)}°C")
    except:
        print(f"   Raw data: {msg.payload.decode()}")

client = mqtt.Client()
client.on_connect = on_connect
client.on_message = on_message

try:
    client.connect("localhost", 1883, 60)
    client.loop_start()
    
    # Publish test message
    test_data = {
        "timestamp": time.time(),
        "deviceId": "test-device",
        "status": "Testing",
        "temperature": 25.0
    }
    
    client.publish("plc/data/factory-io", json.dumps(test_data))
    print("📤 Published test message")
    
    time.sleep(5)
    client.loop_stop()
    client.disconnect()
    print("✅ MQTT test completed")
    
except Exception as e:
    print(f"❌ MQTT test failed: {e}")
"@
    
    $testScript | Out-File -FilePath "mqtt_test.py" -Encoding UTF8
    python mqtt_test.py
    Remove-Item "mqtt_test.py" -ErrorAction SilentlyContinue
}

function Start-Bridge {
    Write-Host "`n🌉 Starting Factory I/O to CloudHMI bridge..." -ForegroundColor Yellow
    
    if (Test-Path "scripts\factory_io_bridge.py") {
        Write-Host "Starting bridge service..." -ForegroundColor Cyan
        Write-Host "Press Ctrl+C to stop the bridge" -ForegroundColor Yellow
        Write-Host "" -ForegroundColor White
        
        try {
            python scripts\factory_io_bridge.py
        } catch {
            Write-Host "❌ Bridge failed to start" -ForegroundColor Red
        }
    } else {
        Write-Host "❌ Bridge script not found: scripts\factory_io_bridge.py" -ForegroundColor Red
    }
}

function Show-Instructions {
    Write-Host "`n📋 Factory I/O Setup Instructions" -ForegroundColor Cyan
    Write-Host "=================================" -ForegroundColor Cyan
    
    Write-Host "`n1. Install Factory I/O:" -ForegroundColor White
    Write-Host "   - Download from: https://factoryio.com/" -ForegroundColor Gray
    Write-Host "   - Run installer as Administrator" -ForegroundColor Gray
    Write-Host "   - Complete installation" -ForegroundColor Gray
    
    Write-Host "`n2. Configure Factory I/O:" -ForegroundColor White
    Write-Host "   - Open Factory I/O" -ForegroundColor Gray
    Write-Host "   - Go to File -> Drivers -> Modbus TCP" -ForegroundColor Gray
    Write-Host "   - Set IP: 127.0.0.1, Port: 502" -ForegroundColor Gray
    Write-Host "   - Load a demo scene (e.g. Sorting by Height)" -ForegroundColor Gray
    
    Write-Host "`n3. Start CloudHMI Platform:" -ForegroundColor White
    Write-Host "   - Run: .\scripts\factory-io-setup.ps1 -InstallDependencies" -ForegroundColor Gray
    Write-Host "   - Start infrastructure: docker-compose up -d" -ForegroundColor Gray
    Write-Host "   - Run Gateway: dotnet run --project src/SmartCloud.Gateway" -ForegroundColor Gray
    Write-Host "   - Run Dashboard: dotnet run --project src/SmartCloud.Dashboard" -ForegroundColor Gray
    
    Write-Host "`n4. Start Data Bridge:" -ForegroundColor White
    Write-Host "   - Run: .\scripts\factory-io-setup.ps1 -RunBridge" -ForegroundColor Gray
    Write-Host "   - Verify data flow in CloudHMI dashboard" -ForegroundColor Gray
    
    Write-Host "`n🎯 Test URLs:" -ForegroundColor Yellow
    Write-Host "   - CloudHMI Dashboard: http://localhost:5000" -ForegroundColor Cyan
    Write-Host "   - InfluxDB UI: http://localhost:8086" -ForegroundColor Cyan
}

# Main execution
if ($InstallDependencies) {
    if (Test-PythonInstallation) {
        Install-PythonDependencies
    }
} elseif ($RunBridge) {
    if (-not (Start-Infrastructure)) {
        Write-Host "❌ Failed to start infrastructure" -ForegroundColor Red
        exit 1
    }
    Start-Bridge
} elseif ($TestMqtt) {
    Test-MqttConnection
} else {
    # Check system status
    Write-Host "`n🔍 System Status Check" -ForegroundColor Cyan
    Write-Host "=====================" -ForegroundColor Cyan
    
    Test-PythonInstallation | Out-Null
    Test-FactoryIoInstallation | Out-Null
    
    # Show instructions
    Show-Instructions
    
    Write-Host "`n🚀 Quick Start Commands:" -ForegroundColor Green
    Write-Host "   .\scripts\factory-io-setup.ps1 -InstallDependencies" -ForegroundColor White
    Write-Host "   .\scripts\factory-io-setup.ps1 -TestMqtt" -ForegroundColor White
    Write-Host "   .\scripts\factory-io-setup.ps1 -RunBridge" -ForegroundColor White
}
