# Factory I/O Sorting by Weight Simulator
# Simulates realistic factory data for CloudHMI dashboard

param(
    [string]$MqttHost = "192.168.68.83",
    [int]$MqttPort = 31883,
    [int]$DurationMinutes = 30
)

Write-Host "Factory I/O - Sorting by Weight Simulator" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "MQTT Target: ${MqttHost}:${MqttPort}"
Write-Host "Duration: ${DurationMinutes} minutes"
Write-Host "Simulating realistic factory data..."
Write-Host ""

# Load configuration
$configPath = Join-Path $PSScriptRoot ".." "config" "k8s-config.ps1"
if (Test-Path $configPath) {
    . $configPath
    Write-Host "CloudHMI Configuration Loaded" -ForegroundColor Cyan
}

# Simulation parameters
$partWeight = 50  # Starting weight
$lightThreshold = 150  # grams
$heavyThreshold = 300  # grams
$lightPartsCounter = 0
$heavyPartsCounter = 0
$totalPartsCounter = 0
$conveyorSpeed = 120  # RPM
$cycleTime = 0

# Function to simulate weight reading with realistic variations
function Get-SimulatedWeight {
    param([int]$baseWeight)
    # Add realistic noise (+/- 5g)
    $noise = Get-Random -Minimum -5 -Maximum 5
    return [Math]::Max(0, $baseWeight + $noise)
}

# Function to send MQTT message using PowerShell HTTP method
function Send-MqttMessage {
    param(
        [string]$Topic,
        [string]$Payload,
        [string]$Host = $MqttHost,
        [int]$Port = $MqttPort
    )
    
    try {
        # For now, just display the message since we don't have mosquitto_pub
        Write-Host "$(Get-Date -Format 'HH:mm:ss') [→] MQTT: $Topic" -ForegroundColor Cyan
        Write-Host "    Payload: $Payload" -ForegroundColor Gray
        
        # In a real setup, this would publish to MQTT broker
        # mosquitto_pub -h $Host -p $Port -t $Topic -m $Payload
    }
    catch {
        Write-Host "$(Get-Date -Format 'HH:mm:ss') [✗] MQTT Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Main simulation loop
$startTime = Get-Date
$endTime = $startTime.AddMinutes($DurationMinutes)

Write-Host "Starting Factory I/O simulation..." -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop" -ForegroundColor Yellow
Write-Host ""

while ((Get-Date) -lt $endTime) {
    $currentTime = Get-Date
    
    # Simulate part entry every 5-15 seconds
    $cycleInterval = Get-Random -Minimum 5 -Maximum 15
    
    # Generate realistic part weight
    $weightVariations = @(80, 120, 180, 250, 320, 400)  # Different part types
    $currentWeight = $weightVariations | Get-Random
    $measuredWeight = Get-SimulatedWeight -baseWeight $currentWeight
    
    # Determine part classification
    $partType = "unknown"
    $destination = "reject"
    
    if ($measuredWeight -lt $lightThreshold) {
        $partType = "light"
        $destination = "light_bin"
        $lightPartsCounter++
    }
    elseif ($measuredWeight -lt $heavyThreshold) {
        $partType = "medium"
        $destination = "heavy_bin"
        $heavyPartsCounter++
    }
    else {
        $partType = "heavy"
        $destination = "heavy_bin"
        $heavyPartsCounter++
    }
    
    $totalPartsCounter++
    $cycleTime = Get-Random -Minimum 3500 -Maximum 6500  # 3.5-6.5 seconds
    
    # Create factory data JSON
    $factoryData = @{
        timestamp = $currentTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
        device_id = "factory-io-sorter"
        scene = "sorting_by_weight"
        weight_reading = $measuredWeight
        weight_threshold_light = $lightThreshold
        weight_threshold_heavy = $heavyThreshold
        part_type = $partType
        destination = $destination
        conveyor_speed = $conveyorSpeed
        cycle_time_ms = $cycleTime
        counters = @{
            light_parts = $lightPartsCounter
            heavy_parts = $heavyPartsCounter
            total_parts = $totalPartsCounter
        }
        sensors = @{
            entry_sensor = $true
            weight_sensor = $true
            light_exit_sensor = ($partType -eq "light")
            heavy_exit_sensor = ($partType -ne "light")
        }
        actuators = @{
            entry_conveyor = $true
            scale_conveyor = $true
            light_conveyor = ($partType -eq "light")
            heavy_conveyor = ($partType -ne "light")
            diverter = ($partType -ne "light")
        }
        status = "running"
        efficiency = [Math]::Round((($lightPartsCounter + $heavyPartsCounter) * 100.0 / [Math]::Max(1, $totalPartsCounter)), 1)
    } | ConvertTo-Json -Compress
    
    # Send to MQTT
    Send-MqttMessage -Topic "plc/factory-io/data" -Payload $factoryData
    
    # Display status
    Write-Host "$(Get-Date -Format 'HH:mm:ss') Weight: ${measuredWeight}g -> $partType ($destination) | Light: $lightPartsCounter | Heavy: $heavyPartsCounter | Total: $totalPartsCounter"
    
    # Wait for next cycle
    Start-Sleep -Seconds $cycleInterval
}

Write-Host ""
Write-Host "Factory I/O simulation completed!" -ForegroundColor Green
Write-Host "Final Statistics:" -ForegroundColor Cyan
Write-Host "  Light parts: $lightPartsCounter"
Write-Host "  Heavy parts: $heavyPartsCounter"
Write-Host "  Total parts: $totalPartsCounter"
Write-Host "  Efficiency: $([Math]::Round((($lightPartsCounter + $heavyPartsCounter) * 100.0 / [Math]::Max(1, $totalPartsCounter)), 1))%"
