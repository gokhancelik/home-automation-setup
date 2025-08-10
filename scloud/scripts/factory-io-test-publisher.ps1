# Factory I/O Test Data Publisher for CloudHMI
# Simulates realistic Factory I/O data when actual simulation is not available

param(
    [string]$BrokerHost = "localhost",
    [int]$BrokerPort = 1883,
    [string]$SceneType = "sorting_by_height", # sorting_by_height, pick_and_place, buffer_station
    [int]$IntervalSeconds = 2,
    [int]$DurationMinutes = 30,
    [switch]$WithAlarms
)

Write-Host "🏭 Factory I/O Test Data Publisher for CloudHMI" -ForegroundColor Green
Write-Host "===============================================" -ForegroundColor Green
Write-Host "Scene: $SceneType" -ForegroundColor Cyan
Write-Host "Publishing to: $BrokerHost`:$BrokerPort" -ForegroundColor Cyan
Write-Host "Interval: $IntervalSeconds seconds, Duration: $DurationMinutes minutes" -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop" -ForegroundColor Yellow
Write-Host ""

# Scene-specific configurations
$sceneConfigs = @{
    "sorting_by_height" = @{
        name = "Sorting by Height Production Line"
        baseSpeed = 150  # RPM
        cycleTime = 12   # seconds
        efficiency = 95  # percent
    }
    "pick_and_place" = @{
        name = "Pick and Place Robot Cell"
        baseSpeed = 80   # cycles per hour
        cycleTime = 45   # seconds
        efficiency = 92  # percent
    }
    "buffer_station" = @{
        name = "Buffer and Accumulation Station"
        baseSpeed = 200  # parts per hour
        cycleTime = 18   # seconds
        efficiency = 98  # percent
    }
}

$config = $sceneConfigs[$SceneType]
$endTime = (Get-Date).AddMinutes($DurationMinutes)
$cycleCount = 0
$totalPartsProduced = 0
$goodParts = 0
$rejectedParts = 0
$alarmActive = $false
$lastAlarmTime = (Get-Date).AddMinutes(-10)

# State variables for realistic simulation
$machineState = "Running"
$conveyorRunning = $true
$temperature = 22.0
$pressure = 6.2
$vibration = 1.2

Write-Host "Starting simulation: $($config.name)" -ForegroundColor Green

try {
    while ((Get-Date) -lt $endTime) {
        $cycleCount++
        $currentTime = Get-Date
        
        # Simulate machine state changes
        $randomEvent = Get-Random -Minimum 1 -Maximum 1000
        
        if ($randomEvent -le 2 -and $WithAlarms) {
            # 0.2% chance of alarm
            $alarmActive = $true
            $machineState = "Alarm"
            $conveyorRunning = $false
            $lastAlarmTime = $currentTime
            Write-Host "⚠️  ALARM: Equipment malfunction detected!" -ForegroundColor Red
        } elseif ($randomEvent -le 5) {
            # 0.5% chance of maintenance mode
            $machineState = "Maintenance"
            $conveyorRunning = $false
            Write-Host "🔧 Entering maintenance mode" -ForegroundColor Yellow
        } elseif ($alarmActive -and ($currentTime - $lastAlarmTime).TotalSeconds -gt 30) {
            # Clear alarm after 30 seconds
            $alarmActive = $false
            $machineState = "Running"
            $conveyorRunning = $true
            Write-Host "✅ Alarm cleared, resuming operation" -ForegroundColor Green
        } elseif ($machineState -eq "Maintenance" -and $randomEvent -gt 950) {
            # 5% chance to exit maintenance
            $machineState = "Running"
            $conveyorRunning = $true
            Write-Host "✅ Maintenance completed, resuming operation" -ForegroundColor Green
        }
        
        # Generate realistic sensor data based on scene type
        switch ($SceneType) {
            "sorting_by_height" {
                $entryPartDetected = $conveyorRunning -and ((Get-Random -Minimum 1 -Maximum 100) -le 70)
                $heightSensorActive = $entryPartDetected -and ((Get-Random -Minimum 1 -Maximum 100) -le 60)
                $exitPartDetected = $entryPartDetected -and ((Get-Random -Minimum 1 -Maximum 100) -le 80)
                
                if ($entryPartDetected) {
                    $totalPartsProduced++
                    if ($heightSensorActive) {
                        $goodParts++
                    } else {
                        $rejectedParts++
                    }
                }
                
                $partDetails = @{
                    entryPartDetected = $entryPartDetected
                    heightSensorActive = $heightSensorActive
                    exitPartDetected = $exitPartDetected
                    sorterActuator = $heightSensorActive
                }
            }
            
            "pick_and_place" {
                $partPresent = $conveyorRunning -and ((Get-Random -Minimum 1 -Maximum 100) -le 50)
                $gripperClosed = $partPresent -and ((Get-Random -Minimum 1 -Maximum 100) -le 80)
                $robotAtHome = -not $partPresent
                $robotAtPlace = $gripperClosed -and ((Get-Random -Minimum 1 -Maximum 100) -le 90)
                
                if ($robotAtPlace) {
                    $totalPartsProduced++
                    if ((Get-Random -Minimum 1 -Maximum 100) -le $config.efficiency) {
                        $goodParts++
                    } else {
                        $rejectedParts++
                    }
                }
                
                $robotX = if ($robotAtHome) { 0.0 } else { [Math]::Round((Get-Random -Minimum -500 -Maximum 500) / 10.0, 1) }
                $robotY = if ($robotAtHome) { 0.0 } else { [Math]::Round((Get-Random -Minimum -300 -Maximum 300) / 10.0, 1) }
                $robotZ = if ($robotAtHome) { 0.0 } else { [Math]::Round((Get-Random -Minimum 0 -Maximum 200) / 10.0, 1) }
                
                $partDetails = @{
                    partPresent = $partPresent
                    gripperClosed = $gripperClosed
                    robotAtHome = $robotAtHome
                    robotAtPlace = $robotAtPlace
                    robotPosition = @{
                        x = $robotX
                        y = $robotY
                        z = $robotZ
                    }
                }
            }
            
            "buffer_station" {
                $bufferLevel = Get-Random -Minimum 0 -Maximum 20
                $bufferEmpty = $bufferLevel -eq 0
                $bufferFull = $bufferLevel -ge 18
                $infeedSensor = $conveyorRunning -and ((Get-Random -Minimum 1 -Maximum 100) -le 40)
                $outfeedSensor = (-not $bufferEmpty) -and ((Get-Random -Minimum 1 -Maximum 100) -le 30)
                
                if ($outfeedSensor) {
                    $totalPartsProduced++
                    $goodParts++
                }
                
                $partDetails = @{
                    bufferLevel = $bufferLevel
                    bufferEmpty = $bufferEmpty
                    bufferFull = $bufferFull
                    infeedSensor = $infeedSensor
                    outfeedSensor = $outfeedSensor
                    maxCapacity = 20
                    efficiency = [Math]::Round(($goodParts / [Math]::Max($totalPartsProduced, 1)) * 100, 1)
                }
            }
        }
        
        # Generate environmental data with realistic variations
        $timeOfDay = (Get-Date).Hour
        $baseTemp = 22 + [Math]::Sin($timeOfDay * [Math]::PI / 12) * 5  # Temperature varies throughout day
        $temperature = [Math]::Round($baseTemp + (Get-Random -Minimum -2 -Maximum 3) + ($conveyorRunning ? 5 : 0), 1)
        
        $pressure = [Math]::Round(6.2 + [Math]::Sin((Get-Date).Minute / 30.0 * [Math]::PI) * 0.5 + (Get-Random -Minimum -0.2 -Maximum 0.2), 2)
        
        $vibration = if ($conveyorRunning) {
            [Math]::Round(1.2 + [Math]::Sin((Get-Date).Second / 10.0 * [Math]::PI) * 0.3 + (Get-Random -Minimum -0.1 -Maximum 0.2), 2)
        } else {
            [Math]::Round(0.1 + (Get-Random -Minimum 0 -Maximum 0.1), 2)
        }
        
        $speed = if ($conveyorRunning) {
            [Math]::Round($config.baseSpeed + (Get-Random -Minimum -10 -Maximum 15))
        } else {
            0
        }
        
        # Calculate cycle time with realistic variations
        $currentCycleTime = if ($conveyorRunning) {
            [Math]::Round($config.cycleTime + (Get-Random -Minimum -2 -Maximum 5), 1)
        } else {
            0.0
        }
        
        # Create CloudHMI-compatible MQTT message
        $payload = @{
            timestamp = (Get-Date -Format "yyyy-MM-ddTHH:mm:ss.fffZ")
            deviceId = "factory-io-simulator"
            machineId = "production-line-01"
            sceneType = $SceneType
            status = $machineState
            temperature = $temperature
            pressure = $pressure
            vibration = $vibration
            speed = $speed
            cycleTime = $currentCycleTime
            partCounter = $totalPartsProduced
            goodParts = $goodParts
            rejectedParts = $rejectedParts
            efficiency = [Math]::Round(($goodParts / [Math]::Max($totalPartsProduced, 1)) * 100, 1)
            
            # Scene-specific data
            sceneData = $partDetails
            
            # Sensor states
            sensors = @{
                conveyorMotor = $conveyorRunning
                emergencyStop = $alarmActive
                temperatureSensor = $temperature
                pressureSensor = $pressure
                vibrationSensor = $vibration
            }
            
            # Alarms
            alarms = @(
                @{
                    id = "TEMP_HIGH"
                    severity = "Warning"
                    active = $temperature -gt 80
                    message = "High temperature detected: $temperature°C"
                },
                @{
                    id = "TEMP_LOW"
                    severity = "Warning"
                    active = $temperature -lt 10
                    message = "Low temperature detected: $temperature°C"
                },
                @{
                    id = "PRESSURE_HIGH"
                    severity = "Warning"
                    active = $pressure -gt 8.0
                    message = "High pressure detected: $pressure bar"
                },
                @{
                    id = "VIBRATION_HIGH"
                    severity = "Critical"
                    active = $vibration -gt 3.0
                    message = "Excessive vibration detected: $vibration mm/s"
                },
                @{
                    id = "EQUIPMENT_FAULT"
                    severity = "Critical"
                    active = $alarmActive
                    message = "Equipment malfunction - immediate attention required"
                },
                @{
                    id = "CYCLE_TIME_WARNING"
                    severity = "Warning"
                    active = $currentCycleTime -gt ($config.cycleTime * 1.5)
                    message = "Cycle time exceeded normal range: $currentCycleTime s"
                }
            )
        } | ConvertTo-Json -Depth 5 -Compress
        
        # Publish to MQTT
        $topic = "plc/data/factory-io"
        
        try {
            # Try using mosquitto_pub first, fall back to docker if not available
            $command = "mosquitto_pub -h $BrokerHost -p $BrokerPort -t `"$topic`" -m `"$payload`""
            $result = Invoke-Expression $command 2>&1
            
            if ($LASTEXITCODE -ne 0) {
                # Fallback to docker mosquitto
                $escapedPayload = $payload -replace '"', '\"'
                $dockerCommand = "docker run --rm --network cloudhmi-network eclipse-mosquitto mosquitto_pub -h cloudhmi-mqtt -t `"$topic`" -m `"$escapedPayload`""
                Invoke-Expression $dockerCommand
            }
            
            # Status output
            $statusColor = switch ($machineState) {
                "Running" { "Green" }
                "Maintenance" { "Yellow" }
                "Alarm" { "Red" }
                default { "White" }
            }
            
            $activeAlarms = ($payload | ConvertFrom-Json).alarms | Where-Object { $_.active -eq $true }
            $alarmText = if ($activeAlarms) { " [⚠️ $($activeAlarms.Count) alarms]" } else { "" }
            
            Write-Host "$(Get-Date -Format 'HH:mm:ss') - $($config.name): $machineState | Parts: $totalPartsProduced (Good: $goodParts, Rejected: $rejectedParts) | Temp: $temperature°C$alarmText" -ForegroundColor $statusColor
            
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
    Write-Host ""
    Write-Host "🏁 Factory I/O simulation completed after $cycleCount cycles" -ForegroundColor Green
    Write-Host "📊 Final Statistics:" -ForegroundColor Cyan
    Write-Host "   Total Parts Produced: $totalPartsProduced" -ForegroundColor White
    Write-Host "   Good Parts: $goodParts" -ForegroundColor Green
    Write-Host "   Rejected Parts: $rejectedParts" -ForegroundColor Red
    Write-Host "   Overall Efficiency: $([Math]::Round(($goodParts / [Math]::Max($totalPartsProduced, 1)) * 100, 1))%" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "💡 Usage Examples:" -ForegroundColor Yellow
Write-Host "   .\scripts\factory-io-test-publisher.ps1 -SceneType sorting_by_height" -ForegroundColor White
Write-Host "   .\scripts\factory-io-test-publisher.ps1 -SceneType pick_and_place -WithAlarms" -ForegroundColor White
Write-Host "   .\scripts\factory-io-test-publisher.ps1 -SceneType buffer_station -IntervalSeconds 1" -ForegroundColor White
