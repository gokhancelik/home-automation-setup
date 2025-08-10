# Simple Factory I/O Test Data Publisher for CloudHMI
# This script generates test data similar to what Factory I/O would produce

param(
    [string]$BrokerHost = "localhost",
    [int]$BrokerPort = 1883,
    [string]$SceneType = "sorting_by_height",
    [int]$IntervalSeconds = 2,
    [int]$DurationMinutes = 30
)

Write-Host "Factory I/O Test Data Publisher for CloudHMI" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host "Scene: $SceneType" -ForegroundColor Cyan
Write-Host "Publishing to: $BrokerHost`:$BrokerPort" -ForegroundColor Cyan
Write-Host "Interval: $IntervalSeconds seconds, Duration: $DurationMinutes minutes" -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop" -ForegroundColor Yellow
Write-Host ""

$endTime = (Get-Date).AddMinutes($DurationMinutes)
$cycleCount = 0
$totalPartsProduced = 0
$goodParts = 0
$rejectedParts = 0

Write-Host "Starting Factory I/O simulation..." -ForegroundColor Green

while ((Get-Date) -lt $endTime) {
    $cycleCount++
    
    # Generate realistic factory data
    $conveyorRunning = $true
    $tempBase = 22 + (Get-Random -Minimum -3 -Maximum 8)
    if ($conveyorRunning) { $tempBase += 5 }
    $temperature = [Math]::Round($tempBase, 1)
    
    $pressure = [Math]::Round(6.2 + (Get-Random -Minimum -0.5 -Maximum 0.5), 2)
    $vibration = [Math]::Round(1.2 + (Get-Random -Minimum -0.3 -Maximum 0.4), 2)
    $speed = if ($conveyorRunning) { Get-Random -Minimum 140 -Maximum 160 } else { 0 }
    
    # Simulate parts production
    $partDetected = (Get-Random -Minimum 1 -Maximum 100) -le 60
    if ($partDetected) {
        $totalPartsProduced++
        if ((Get-Random -Minimum 1 -Maximum 100) -le 90) {
            $goodParts++
        } else {
            $rejectedParts++
        }
    }
    
    # Create MQTT payload
    $payload = @{
        timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ss.fffZ")
        deviceId = "factory-io-simulator"
        machineId = "production-line-01"
        sceneType = $SceneType
        status = "Running"
        temperature = $temperature
        pressure = $pressure
        vibration = $vibration
        speed = $speed
        partCounter = $totalPartsProduced
        goodParts = $goodParts
        rejectedParts = $rejectedParts
        efficiency = [Math]::Round(($goodParts / [Math]::Max($totalPartsProduced, 1)) * 100, 1)
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
                active = $temperature -gt 80
                message = "High temperature: $temperature C"
            }
        )
    } | ConvertTo-Json -Depth 3 -Compress
    
    # Publish to MQTT
    $topic = "plc/data/factory-io"
    
    try {
        # Try mosquitto_pub command
        $command = "mosquitto_pub -h $BrokerHost -p $BrokerPort -t `"$topic`" -m `"$payload`""
        Invoke-Expression $command
        
        Write-Host "$(Get-Date -Format 'HH:mm:ss') - Cycle $cycleCount`: Parts: $totalPartsProduced (Good: $goodParts) | Temp: $temperature C | Speed: $speed RPM" -ForegroundColor Green
    }
    catch {
        Write-Host "$(Get-Date -Format 'HH:mm:ss') - Failed to publish (broker may not be running): $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
    Start-Sleep -Seconds $IntervalSeconds
}

Write-Host ""
Write-Host "Factory I/O simulation completed!" -ForegroundColor Green
Write-Host "Total cycles: $cycleCount" -ForegroundColor Cyan
Write-Host "Parts produced: $totalPartsProduced (Good: $goodParts, Rejected: $rejectedParts)" -ForegroundColor Cyan
Write-Host "Efficiency: $([Math]::Round(($goodParts / [Math]::Max($totalPartsProduced, 1)) * 100, 1))%" -ForegroundColor Cyan
