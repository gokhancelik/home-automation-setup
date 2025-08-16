# Update CloudHMI appsettings.json files for Kubernetes deployment

# Load K8s configuration
. "$PSScriptRoot\..\config\k8s-config.ps1"

Write-Host "Updating CloudHMI Configuration for Kubernetes" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green

# Update Gateway appsettings.json
if (Test-Path "src/SmartCloud.Gateway/appsettings.json") {
    Write-Host "Updating Gateway settings..." -ForegroundColor Cyan
    
    $gatewaySettings = @{
        "Logging" = @{
            "LogLevel" = @{
                "Default" = "Information"
                "Microsoft.AspNetCore" = "Warning"
            }
        }
        "MqttSettings" = @{
            "Host" = $global:MqttBrokerHost
            "Port" = $global:MqttBrokerPort
            "ClientId" = "CloudHMI-Gateway"
            "Topic" = "plc/data/+"
        }
        "InfluxDbSettings" = @{
            "Host" = $global:InfluxDbHost
            "Port" = $global:InfluxDbPort
            "Organization" = $global:InfluxDbOrg
            "Bucket" = $global:InfluxDbBucket
            "Username" = $global:InfluxDbUser
            "Password" = $global:InfluxDbPassword
        }
        "AllowedHosts" = "*"
    }
    
    $gatewaySettings | ConvertTo-Json -Depth 4 | Set-Content "src/SmartCloud.Gateway/appsettings.json"
    Write-Host "✅ Gateway settings updated" -ForegroundColor Green
}

# Update Dashboard appsettings.json  
if (Test-Path "src/SmartCloud.Dashboard/appsettings.json") {
    Write-Host "Updating Dashboard settings..." -ForegroundColor Cyan
    
    $dashboardSettings = @{
        "Logging" = @{
            "LogLevel" = @{
                "Default" = "Information"
                "Microsoft.AspNetCore" = "Warning"
            }
        }
        "InfluxDbSettings" = @{
            "Host" = $global:InfluxDbHost
            "Port" = $global:InfluxDbPort
            "Organization" = $global:InfluxDbOrg
            "Bucket" = $global:InfluxDbBucket
            "Username" = $global:InfluxDbUser
            "Password" = $global:InfluxDbPassword
        }
        "AllowedHosts" = "*"
    }
    
    $dashboardSettings | ConvertTo-Json -Depth 4 | Set-Content "src/SmartCloud.Dashboard/appsettings.json"
    Write-Host "✅ Dashboard settings updated" -ForegroundColor Green
}

Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host "1. Test K8s connectivity:" -ForegroundColor White
Write-Host "   .\scripts\k8s-factory-bridge.ps1 -TestConnection" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Start Factory I/O simulation:" -ForegroundColor White
Write-Host "   .\scripts\k8s-factory-bridge.ps1 -DurationMinutes 5" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Build and run CloudHMI:" -ForegroundColor White
Write-Host "   dotnet build" -ForegroundColor Gray
Write-Host "   dotnet run --project src/SmartCloud.Gateway" -ForegroundColor Gray
Write-Host "   dotnet run --project src/SmartCloud.Dashboard" -ForegroundColor Gray
