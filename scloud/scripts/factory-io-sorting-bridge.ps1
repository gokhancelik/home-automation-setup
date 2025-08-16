# Factory I/O Sorting by Weight Scene - MQTT Bridge for CloudHMI
# Simulates realistic data from the Sorting by Weight scene

param(
    [string]$MqttHost = "192.168.68.83",
    [int]$MqttPort = 31883,
    [int]$IntervalSeconds = 2,
    [int]$DurationMinutes = 30,
    [switch]$TestMode
)

# Load K8s configuration
if (Test-Path "$PSScriptRoot\..\config\k8s-config.ps1") {
    . "$PSScriptRoot\..\config\k8s-config.ps1"
    if ($global:MqttBrokerHost) { $MqttHost = $global:MqttBrokerHost }
    if ($global:MqttBrokerPort) { $MqttPort = $global:MqttBrokerPort }
}

Write-Host "Factory I/O - Sorting by Weight Scene Simulator" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
Write-Host "Target: $MqttHost`:$MqttPort" -ForegroundColor Cyan
Write-Host "Duration: $DurationMinutes minutes" -ForegroundColor Cyan
Write-Host "Test Mode: $TestMode" -ForegroundColor Cyan
Write-Host ""

# Scene-specific variables
$endTime = (Get-Date).AddMinutes($DurationMinutes)
$cycleCount = 0
$lightPartsCount = 0
$heavyPartsCount = 0
$totalPartsCount = 0
$rejectCount = 0

# Sorting parameters
$weightThreshold = 150  # grams - parts below this are "light", above are "heavy"
$conveyorSpeed = 120    # RPM
$sceneActive = $true

Write-Host "Weight Threshold: $weightThreshold grams" -ForegroundColor Yellow
Write-Host "Conveyor Speed: $conveyorSpeed RPM" -ForegroundColor Yellow
Write-Host ""

while ((Get-Date) -lt $endTime -and $sceneActive) {
    $cycleCount++
    $currentTime = Get-Date
    
    # Simulate part arrival (60% chance per cycle)
    $partPresent = (Get-Random -Minimum 1 -Maximum 100) -le 60
    
    if ($partPresent) {
        $totalPartsCount++
        
        # Generate realistic weight (normal distribution around 100g and 200g)
        $randomType = Get-Random -Minimum 1 -Maximum 100
        if ($randomType -le 40) {
            # Light part (60-140g)
            $currentWeight = Get-Random -Minimum 60 -Maximum 140
        } elseif ($randomType -le 80) {
            # Heavy part (160-300g)
            $currentWeight = Get-Random -Minimum 160 -Maximum 300
        } else {
            # Edge case / damaged part (could be rejected)
            $currentWeight = Get-Random -Minimum 10 -Maximum 50
        }
        
        # Determine sorting outcome
        if ($currentWeight -lt 50) {
            # Reject - too light (damaged/incomplete)
            $rejectCount++
            $sortedTo = "reject"
            $diverterActive = $true
        } elseif ($currentWeight -lt $weightThreshold) {
            # Light part
            $lightPartsCount++
            $sortedTo = "light"
            $diverterActive = $false
        } else {
            # Heavy part
            $heavyPartsCount++
            $sortedTo = "heavy"
            $diverterActive = $true
        }
    } else {
        $currentWeight = 0
        $sortedTo = "none"
        $diverterActive = $false
    }
    
    # Calculate cycle time (varies based on weight and sorting complexity)
    $baseCycleTime = 3000  # 3 seconds base
    $weightPenalty = if ($currentWeight -gt 200) { 500 } else { 0 }  # Heavy parts take longer
    $cycleTime = $baseCycleTime + $weightPenalty + (Get-Random -Minimum -200 -Maximum 400)
    
    # Sensor states
    $sensors = @{
        entryConveyor = $partPresent
        weightScale = $partPresent -and ($currentWeight -gt 0)
        lightPartExit = ($sortedTo -eq "light")
        heavyPartExit = ($sortedTo -eq "heavy")
        rejectBin = ($sortedTo -eq "reject")
    }
    
    # Actuator states
    $actuators = @{
        entryConveyorMotor = $sceneActive
        scaleConveyorMotor = $sceneActive
        lightPartsConveyor = $sceneActive
        heavyPartsConveyor = $sceneActive
        diverterActuator = $diverterActive
    }
    
    # Environmental simulation
    $temperature = [Math]::Round(22 + [Math]::Sin((Get-Date).Minute / 30.0 * [Math]::PI) * 3 + (Get-Random -Minimum -1 -Maximum 2), 1)
    $vibration = [Math]::Round(0.8 + [Math]::Sin($cycleCount / 10.0) * 0.3 + (Get-Random -Minimum -0.1 -Maximum 0.2), 2)
    $powerConsumption = [Math]::Round(15 + ($conveyorSpeed / 10) + ($currentWeight / 50), 1)
    
    # Calculate efficiency
    $efficiency = if ($totalPartsCount -gt 0) { 
        [Math]::Round((($lightPartsCount + $heavyPartsCount) / $totalPartsCount) * 100, 1) 
    } else { 
        100 
    }
    
    # Generate alarms
    $alarms = @()
    
    if ($rejectCount -gt ($totalPartsCount * 0.15)) {
        $alarms += @{
            id = "HIGH_REJECT_RATE"
            severity = "Warning"
            active = $true
            message = "High reject rate detected: $([Math]::Round(($rejectCount / $totalPartsCount) * 100, 1))%"
        }
    }
    
    if ($temperature -gt 28) {
        $alarms += @{
            id = "TEMP_HIGH"
            severity = "Warning"
            active = $true
            message = "Temperature elevated: ${temperature}C"
        }
    }
    
    if ($vibration -gt 1.5) {
        $alarms += @{
            id = "VIBRATION_HIGH"
            severity = "Critical"
            active = $true
            message = "Excessive vibration: $vibration mm/s"
        }
    }
    
    # Create CloudHMI message
    $payload = @{
        timestamp = $currentTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        deviceId = "factory-io-sorting"
        machineId = "sorting-line-weight"
        sceneType = "sorting_by_weight"
        status = if ($sceneActive) { "Running" } else { "Stopped" }
        
        # Process data
        currentWeight = $currentWeight
        weightThreshold = $weightThreshold
        sortingResult = $sortedTo
        cycleTime = $cycleTime
        
        # Production counters
        totalParts = $totalPartsCount
        lightParts = $lightPartsCount
        heavyParts = $heavyPartsCount
        rejectedParts = $rejectCount
        efficiency = $efficiency
        
        # Environmental
        temperature = $temperature
        vibration = $vibration
        powerConsumption = $powerConsumption
        conveyorSpeed = $conveyorSpeed
        
        # Sensors
        sensors = $sensors
        
        # Actuators
        actuators = $actuators
        
        # Alarms
        alarms = $alarms
        
        # Scene-specific metrics
        metrics = @{
            averageWeight = if ($totalPartsCount -gt 0) { 
                [Math]::Round(($lightPartsCount * 100 + $heavyPartsCount * 220) / $totalPartsCount, 1) 
            } else { 0 }
            sortingAccuracy = $efficiency
            throughputPerHour = [Math]::Round($totalPartsCount * (60.0 / [Math]::Max($cycleCount * $IntervalSeconds / 60.0, 1)), 0)
        }
    } | ConvertTo-Json -Depth 4 -Compress
    
    # Publish to MQTT
    $topic = "plc/data/factory-io"
    
    try {
        # Try mosquitto_pub first
        $command = "mosquitto_pub -h $MqttHost -p $MqttPort -t `"$topic`" -m `"$payload`""
        $result = Invoke-Expression $command 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            $statusIcon = switch ($sortedTo) {
                "light" { "[OK]" }
                "heavy" { "[HV]" }
                "reject" { "[RJ]" }
                default { "[--]" }
            }
            
            Write-Host "$(Get-Date -Format 'HH:mm:ss') $statusIcon Weight: $($currentWeight)g -> $sortedTo | Total: $totalPartsCount (L:$lightPartsCount H:$heavyPartsCount R:$rejectCount) | Eff: $efficiency%" -ForegroundColor Green
        } else {
            Write-Host "$(Get-Date -Format 'HH:mm:ss') [X] Failed to publish to MQTT" -ForegroundColor Red
        }
    } catch {
        Write-Host "$(Get-Date -Format 'HH:mm:ss') [!] MQTT Error: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
    Start-Sleep -Seconds $IntervalSeconds
}

Write-Host ""
Write-Host "Sorting by Weight Simulation Complete!" -ForegroundColor Green
Write-Host "Final Statistics:" -ForegroundColor Cyan
Write-Host "   Total Cycles: $cycleCount" -ForegroundColor White
Write-Host "   Parts Processed: $totalPartsCount" -ForegroundColor White
Write-Host "   Light Parts: $lightPartsCount ($([Math]::Round(($lightPartsCount / [Math]::Max($totalPartsCount, 1)) * 100, 1))%)" -ForegroundColor Green
Write-Host "   Heavy Parts: $heavyPartsCount ($([Math]::Round(($heavyPartsCount / [Math]::Max($totalPartsCount, 1)) * 100, 1))%)" -ForegroundColor Yellow
Write-Host "   Rejected Parts: $rejectCount ($([Math]::Round(($rejectCount / [Math]::Max($totalPartsCount, 1)) * 100, 1))%)" -ForegroundColor Red
Write-Host "   Final Efficiency: $efficiency%" -ForegroundColor Cyan
