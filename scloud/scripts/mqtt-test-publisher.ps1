# MQTT Test Data Publisher
# This PowerShell script simulates PLC data by publishing MQTT messages

# Requires: mosquitto_pub (install mosquitto client tools)
# Download from: https://mosquitto.org/download/

param(
    [string]$BrokerHost = "localhost",
    [int]$BrokerPort = 1883,
    [string]$DeviceId = "PLC001",
    [int]$IntervalSeconds = 5,
    [int]$DurationMinutes = 60
)

Write-Host "Starting MQTT test data publisher for device: $DeviceId"
Write-Host "Publishing to: $BrokerHost`:$BrokerPort"
Write-Host "Interval: $IntervalSeconds seconds, Duration: $DurationMinutes minutes"
Write-Host "Press Ctrl+C to stop"

$endTime = (Get-Date).AddMinutes($DurationMinutes)
$cycleCount = 0

try {
    while ((Get-Date) -lt $endTime) {
        $cycleCount++
        
        # Generate realistic PLC data
        $temperature = [Math]::Round((Get-Random -Minimum 65 -Maximum 85) + [Math]::Sin((Get-Date).Minute / 10.0) * 5, 1)
        $pressure = [Math]::Round((Get-Random -Minimum 4.5 -Maximum 6.5) + [Math]::Sin((Get-Date).Second / 30.0) * 0.5, 1)
        $vibration = [Math]::Round((Get-Random -Minimum 0.5 -Maximum 3.0), 2)
        $powerConsumption = [Math]::Round((Get-Random -Minimum 15 -Maximum 25) + [Math]::Sin((Get-Date).Minute / 5.0) * 3, 1)
        $quality = Get-Random -Minimum 90 -Maximum 100
        $isRunning = $true
        
        # Occasionally simulate maintenance or issues
        if ((Get-Random -Minimum 1 -Maximum 100) -le 5) {
            $isRunning = $false
            $quality = Get-Random -Minimum 70 -Maximum 90
        }
        
        # Create JSON payload
        $payload = @{
            device_id = $DeviceId
            timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ss.fffZ")
            temperature = $temperature
            pressure = $pressure
            vibration = $vibration
            power_consumption = $powerConsumption
            cycle_count = $cycleCount
            quality = $quality
            is_running = $isRunning
            location = "Factory_Floor_1"
        } | ConvertTo-Json -Compress
        
        # Publish to MQTT
        $topic = "plc/$DeviceId/data"
        $command = "mosquitto_pub -h $BrokerHost -p $BrokerPort -t `"$topic`" -m `"$payload`""
        
        try {
            Invoke-Expression $command
            Write-Host "$(Get-Date -Format 'HH:mm:ss') - Published data for $DeviceId (cycle: $cycleCount, temp: $temperature°C, quality: $quality%)"
        }
        catch {
            Write-Warning "Failed to publish MQTT message: $($_.Exception.Message)"
        }
        
        Start-Sleep -Seconds $IntervalSeconds
    }
}
catch {
    Write-Error "Script interrupted: $($_.Exception.Message)"
}
finally {
    Write-Host "MQTT test publisher stopped after $cycleCount cycles"
}

Write-Host ""
Write-Host "To run this script:"
Write-Host "  .\scripts\mqtt-test-publisher.ps1"
Write-Host ""
Write-Host "To customize parameters:"
Write-Host "  .\scripts\mqtt-test-publisher.ps1 -DeviceId PLC002 -IntervalSeconds 10"
Write-Host ""
Write-Host "Make sure mosquitto client tools are installed and mosquitto broker is running"
