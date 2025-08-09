# MQTT Test Script for PowerShell
# This script tests the MQTT broker installation

param(
    [string]$BrokerHost = "192.168.68.83",
    [int]$BrokerPort = 31883,
    [string]$Topic = "test/topic",
    [string]$Message = "Hello from PowerShell MQTT test!"
)

Write-Host "MQTT Broker Test Results" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host ""

# Test NodePort connectivity
Write-Host "Testing MQTT NodePort connectivity..." -ForegroundColor Yellow
$mqttTest = Test-NetConnection -ComputerName $BrokerHost -Port $BrokerPort -WarningAction SilentlyContinue
if ($mqttTest.TcpTestSucceeded) {
    Write-Host "✅ MQTT NodePort ($BrokerHost`:$BrokerPort) is accessible" -ForegroundColor Green
} else {
    Write-Host "❌ MQTT NodePort ($BrokerHost`:$BrokerPort) is not accessible" -ForegroundColor Red
}

# Test WebSocket NodePort connectivity
Write-Host "Testing WebSocket NodePort connectivity..." -ForegroundColor Yellow
$wsTest = Test-NetConnection -ComputerName $BrokerHost -Port 31901 -WarningAction SilentlyContinue
if ($wsTest.TcpTestSucceeded) {
    Write-Host "✅ WebSocket NodePort ($BrokerHost`:31901) is accessible" -ForegroundColor Green
} else {
    Write-Host "❌ WebSocket NodePort ($BrokerHost`:31901) is not accessible" -ForegroundColor Red
}

Write-Host ""
Write-Host "Available Access Methods:" -ForegroundColor Yellow
Write-Host "1. 🔗 Internal Cluster: mosquitto.mqtt.svc.cluster.local:1883" -ForegroundColor Cyan
Write-Host "2. 🌐 NodePort MQTT: $BrokerHost`:31883" -ForegroundColor Cyan
Write-Host "3. 🌐 NodePort WebSocket: $BrokerHost`:31901" -ForegroundColor Cyan
Write-Host "4. 🔒 HTTPS WebSocket: https://mqtt.gcelik.dev/" -ForegroundColor Cyan
Write-Host "5. 🧪 Test Page: file:///C:/Projects/home-automation-setup/docs/mqtt-websocket-test.html" -ForegroundColor Cyan

Write-Host ""
if (Get-Command mosquitto_pub -ErrorAction SilentlyContinue) {
    Write-Host "Testing with mosquitto_pub..." -ForegroundColor Yellow
    Write-Host "Publishing test message..." -ForegroundColor Yellow
    & mosquitto_pub -h $BrokerHost -p $BrokerPort -t $Topic -m $Message
    
    Write-Host "To subscribe, run: mosquitto_sub -h $BrokerHost -p $BrokerPort -t $Topic" -ForegroundColor Yellow
} else {
    Write-Host "Mosquitto clients not found. To install:" -ForegroundColor Red
    Write-Host "  - Windows: Download from https://mosquitto.org/download/" -ForegroundColor Yellow
    Write-Host "  - Or use Docker: docker run --rm -it eclipse-mosquitto:2.0 mosquitto_pub -h $BrokerHost -p $BrokerPort -t $Topic -m '$Message'" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Quick Test Commands:" -ForegroundColor Yellow
Write-Host "  Publisher: mosquitto_pub -h $BrokerHost -p $BrokerPort -t $Topic -m 'Hello MQTT'" -ForegroundColor White
Write-Host "  Subscriber: mosquitto_sub -h $BrokerHost -p $BrokerPort -t $Topic" -ForegroundColor White
Write-Host ""
Write-Host "WebSocket Test: Open the test page in your browser to test WebSocket connectivity" -ForegroundColor Yellow
