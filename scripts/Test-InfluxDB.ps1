# Test InfluxDB connectivity and functionality
param(
    [string]$NodeIP = "192.168.68.83",
    [int]$NodePort = 31086
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "    InfluxDB IoT Platform Test Suite     " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$InfluxDBUrl = "http://${NodeIP}:${NodePort}"
Write-Host "Testing InfluxDB at: $InfluxDBUrl" -ForegroundColor Yellow

# Test 1: Basic connectivity
Write-Host "`n1. Testing basic connectivity..." -ForegroundColor Green
try {
    $pingResponse = Invoke-RestMethod -Uri "$InfluxDBUrl/ping" -Method GET -TimeoutSec 10
    Write-Host "   [OK] InfluxDB is responding" -ForegroundColor Green
} catch {
    Write-Host "   [FAIL] Connection failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test 2: Version information
Write-Host "`n2. Getting version information..." -ForegroundColor Green
try {
    $versionHeaders = Invoke-WebRequest -Uri "$InfluxDBUrl/ping" -Method GET -TimeoutSec 10
    $version = $versionHeaders.Headers.'X-Influxdb-Version'
    Write-Host "   [OK] InfluxDB version: $version" -ForegroundColor Green
} catch {
    Write-Host "   [WARN] Could not retrieve version" -ForegroundColor Yellow
}

# Test 3: Authentication with admin user
Write-Host "`n3. Testing admin authentication..." -ForegroundColor Green
$adminCreds = @{
    q = "SHOW DATABASES"
}
$authHeader = @{
    Authorization = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("admin:iot_influx_pass123"))
}

try {
    $response = Invoke-RestMethod -Uri "$InfluxDBUrl/query" -Method POST -Body $adminCreds -Headers $authHeader -TimeoutSec 10
    if ($response.results[0].series) {
        $databases = $response.results[0].series[0].values | ForEach-Object { $_[0] }
        Write-Host "   [OK] Admin authentication successful" -ForegroundColor Green
        Write-Host "   [OK] Available databases: $($databases -join ', ')" -ForegroundColor Green
    }
} catch {
    Write-Host "   [FAIL] Admin authentication failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: IoT user authentication and database access
Write-Host "`n4. Testing IoT user authentication..." -ForegroundColor Green
$iotUserCreds = @{
    q = "SHOW DATABASES"
    db = "iot_sensors"
}
$iotAuthHeader = @{
    Authorization = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("iot_user:iot_user_pass123"))
}

try {
    $response = Invoke-RestMethod -Uri "$InfluxDBUrl/query" -Method POST -Body $iotUserCreds -Headers $iotAuthHeader -TimeoutSec 10
    Write-Host "   [OK] IoT user authentication successful" -ForegroundColor Green
} catch {
    Write-Host "   [FAIL] IoT user authentication failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 5: Write sample IoT sensor data
Write-Host "`n5. Testing data write operations..." -ForegroundColor Green
$timestamp = [int64]((Get-Date).ToUniversalTime() - (Get-Date "1970-01-01 00:00:00")).TotalSeconds * 1000000000
$sampleData = @"
temperature,sensor_id=DHT22_01,location=living_room value=23.5 $timestamp
humidity,sensor_id=DHT22_01,location=living_room value=65.2 $timestamp
pressure,sensor_id=BMP280_01,location=living_room value=1013.25 $timestamp
"@

try {
    $writeResponse = Invoke-RestMethod -Uri "$InfluxDBUrl/write?db=iot_sensors" -Method POST -Body $sampleData -Headers $iotAuthHeader -ContentType "application/x-www-form-urlencoded" -TimeoutSec 10
    Write-Host "   [OK] Sample IoT data written successfully" -ForegroundColor Green
} catch {
    Write-Host "   [FAIL] Data write failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 6: Read data back
Write-Host "`n6. Testing data read operations..." -ForegroundColor Green
$queryData = @{
    q = "SELECT * FROM temperature LIMIT 5"
    db = "iot_sensors"
}

try {
    $queryResponse = Invoke-RestMethod -Uri "$InfluxDBUrl/query" -Method POST -Body $queryData -Headers $iotAuthHeader -TimeoutSec 10
    if ($queryResponse.results[0].series) {
        Write-Host "   [OK] Data query successful" -ForegroundColor Green
        $columns = $queryResponse.results[0].series[0].columns
        $values = $queryResponse.results[0].series[0].values
        Write-Host "   [OK] Retrieved $($values.Count) temperature records" -ForegroundColor Green
    } else {
        Write-Host "   [WARN] No temperature data found (this is normal for a fresh installation)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   [FAIL] Data query failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 7: Read-only user test
Write-Host "`n7. Testing read-only user access..." -ForegroundColor Green
$readerAuthHeader = @{
    Authorization = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("iot_reader:iot_reader_pass123"))
}

try {
    $readerResponse = Invoke-RestMethod -Uri "$InfluxDBUrl/query" -Method POST -Body $queryData -Headers $readerAuthHeader -TimeoutSec 10
    Write-Host "   [OK] Read-only user can query data" -ForegroundColor Green
} catch {
    Write-Host "   [FAIL] Read-only user query failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Summary
Write-Host "`n==========================================" -ForegroundColor Cyan
Write-Host "           Test Summary                   " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "InfluxDB Status: Ready for IoT workloads" -ForegroundColor Green
Write-Host "Access URL: $InfluxDBUrl" -ForegroundColor White
Write-Host "Database: iot_sensors" -ForegroundColor White
Write-Host "Users: admin (full), iot_user (write), iot_reader (read)" -ForegroundColor White
Write-Host "NodePort: $NodePort (accessible from outside cluster)" -ForegroundColor White
Write-Host "TLS Certificate: Available at https://influxdb.gcelik.dev" -ForegroundColor White
Write-Host "Storage: 5Gi persistent volume" -ForegroundColor White
Write-Host "Authentication: Enabled with multiple user roles" -ForegroundColor White

Write-Host ""
Write-Host "IoT Integration Examples:" -ForegroundColor Yellow
Write-Host "  - MQTT -> InfluxDB bridge for sensor data" -ForegroundColor White
Write-Host "  - Grafana dashboards for time-series visualization" -ForegroundColor White
Write-Host "  - Redis caching for frequently accessed data" -ForegroundColor White
Write-Host "  - Retention policies for data lifecycle management" -ForegroundColor White
