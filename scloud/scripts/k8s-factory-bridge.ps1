# CloudHMI Factory I/O Bridge for Kubernetes Setup
# Modified version that reads K8s service IPs from configuration

param(
    [string]$MqttHost = "192.168.68.83",
    [int]$MqttPort = 31883,
    [string]$SceneType = "sorting_by_height",
    [int]$IntervalSeconds = 2,
    [int]$DurationMinutes = 30,
    [switch]$TestConnection
)

# Load K8s configuration
. "$PSScriptRoot\..\config\k8s-config.ps1"

# Override with config values if not provided
if ($MqttHost -eq "192.168.68.83" -and $global:MqttBrokerHost) {
    $MqttHost = $global:MqttBrokerHost
}
if ($MqttPort -eq 31883 -and $global:MqttBrokerPort) {
    $MqttPort = $global:MqttBrokerPort
}

function Test-K8sServices {
    Write-Host "Testing Kubernetes Service Connectivity..." -ForegroundColor Yellow
    Write-Host "==========================================" -ForegroundColor Yellow
    
    # Test MQTT Broker
    Write-Host "Testing MQTT Broker at $MqttHost`:$MqttPort..." -ForegroundColor Cyan
    try {
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $tcpClient.Connect($MqttHost, $MqttPort)
        $tcpClient.Close()
        Write-Host "✅ MQTT Broker is reachable" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ MQTT Broker is not reachable: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Test InfluxDB
    Write-Host "Testing InfluxDB at $global:InfluxDbHost`:$global:InfluxDbPort..." -ForegroundColor Cyan
    try {
        $response = Invoke-WebRequest -Uri "http://$global:InfluxDbHost`:$global:InfluxDbPort/ping" -TimeoutSec 5 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 204) {
            Write-Host "✅ InfluxDB is reachable" -ForegroundColor Green
        }
        else {
            Write-Host "⚠️  InfluxDB responded but may not be fully ready" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "❌ InfluxDB is not reachable: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Test Redis (if configured)
    if ($global:RedisHost -ne "192.168.1.102") {
        Write-Host "Testing Redis at $global:RedisHost`:$global:RedisPort..." -ForegroundColor Cyan
        try {
            $tcpClient = New-Object System.Net.Sockets.TcpClient
            $tcpClient.Connect($global:RedisHost, $global:RedisPort)
            $tcpClient.Close()
            Write-Host "✅ Redis is reachable" -ForegroundColor Green
        }
        catch {
            Write-Host "❌ Redis is not reachable: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
    Write-Host ""
}

function Start-FactorySimulation {
    Write-Host "🏭 Factory I/O Simulation for Kubernetes CloudHMI" -ForegroundColor Green
    Write-Host "=================================================" -ForegroundColor Green
    Write-Host "Scene: $SceneType" -ForegroundColor Cyan
    Write-Host "MQTT Target: $MqttHost`:$MqttPort" -ForegroundColor Cyan
    Write-Host "Duration: $DurationMinutes minutes" -ForegroundColor Cyan
    Write-Host ""

    $endTime = (Get-Date).AddMinutes($DurationMinutes)
    $cycleCount = 0
    $totalPartsProduced = 0
    $goodParts = 0

    while ((Get-Date) -lt $endTime) {
        $cycleCount++
        
        # Generate factory data
        $conveyorRunning = $true
        $temperature = [Math]::Round(22 + (Get-Random -Minimum -3 -Maximum 8), 1)
        $pressure = [Math]::Round(6.2 + (Get-Random -Minimum -0.5 -Maximum 0.5), 2)
        $vibration = [Math]::Round(1.2 + (Get-Random -Minimum -0.3 -Maximum 0.4), 2)
        $speed = 150 + (Get-Random -Minimum -10 -Maximum 15)
        
        # Parts production simulation
        $partDetected = (Get-Random -Minimum 1 -Maximum 100) -le 60
        if ($partDetected) {
            $totalPartsProduced++
            if ((Get-Random -Minimum 1 -Maximum 100) -le 90) {
                $goodParts++
            }
        }
        
        # Create CloudHMI message
        $payload = @{
            timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ss.fffZ")
            deviceId = "factory-io-k8s"
            machineId = "production-line-01"
            location = "kubernetes-cluster"
            status = "Running"
            temperature = $temperature
            pressure = $pressure
            vibration = $vibration
            speed = $speed
            partCounter = $totalPartsProduced
            goodParts = $goodParts
            efficiency = if ($totalPartsProduced -gt 0) { [Math]::Round(($goodParts / $totalPartsProduced) * 100, 1) } else { 0 }
            sensors = @{
                conveyorMotor = $conveyorRunning
                partDetected = $partDetected
                temperatureSensor = $temperature
                pressureSensor = $pressure
            }
            alarms = @(
                @{
                    id = "TEMP_HIGH"
                    severity = "Warning"
                    active = $temperature -gt 35
                    message = "Temperature elevated: $temperature°C"
                }
            )
        } | ConvertTo-Json -Depth 3 -Compress
        
        # Publish to K8s MQTT
        $topic = "plc/data/factory-io"
        
        try {
            # Use mosquitto_pub with K8s IP
            $command = "mosquitto_pub -h $MqttHost -p $MqttPort -t `"$topic`" -m `"$payload`""
            $result = Invoke-Expression $command 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "$(Get-Date -Format 'HH:mm:ss') ✅ K8s Cycle $cycleCount`: Parts: $totalPartsProduced (Good: $goodParts) | Temp: $temperature°C | Speed: $speed RPM" -ForegroundColor Green
            }
            else {
                Write-Host "$(Get-Date -Format 'HH:mm:ss') ❌ Failed to publish to K8s MQTT" -ForegroundColor Red
            }
        }
        catch {
            Write-Host "$(Get-Date -Format 'HH:mm:ss') ⚠️  MQTT publish error: $($_.Exception.Message)" -ForegroundColor Yellow
        }
        
        Start-Sleep -Seconds $IntervalSeconds
    }
    
    Write-Host ""
    Write-Host "🏁 K8s Factory Simulation Complete!" -ForegroundColor Green
    Write-Host "Cycles: $cycleCount | Parts: $totalPartsProduced | Efficiency: $([Math]::Round(($goodParts / [Math]::Max($totalPartsProduced, 1)) * 100, 1))%" -ForegroundColor Cyan
}

# Main execution
if ($TestConnection) {
    Test-K8sServices
}
else {
    # First test connectivity
    Test-K8sServices
    
    # If MQTT is reachable, start simulation
    try {
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $tcpClient.Connect($MqttHost, $MqttPort)
        $tcpClient.Close()
        
        Write-Host "Starting simulation in 3 seconds..." -ForegroundColor Yellow
        Start-Sleep -Seconds 3
        Start-FactorySimulation
    }
    catch {
        Write-Host "❌ Cannot start simulation - MQTT broker not accessible" -ForegroundColor Red
        Write-Host "Please update the IP addresses in config\k8s-config.ps1" -ForegroundColor Yellow
    }
}
