# Kubernetes Configuration for CloudHMI
# Update these values with your K8s cluster IPs

# MQTT Broker (Mosquitto)
$global:MqttBrokerHost = "192.168.68.83"
$global:MqttBrokerPort = 31883

# InfluxDB
$global:InfluxDbHost = "192.168.68.83"
$global:InfluxDbPort = 31086
$global:InfluxDbOrg = "cloudhmi"
$global:InfluxDbBucket = "iot_sensors"
$global:InfluxDbToken = "your-influxdb-token"  # Generate from InfluxDB UI at http://192.168.68.83:31086
$global:InfluxDbUser = "iot_user"
$global:InfluxDbPassword = "iot_user_pass123"

# Redis Cache
$global:RedisHost = "192.168.68.83"
$global:RedisPort = 31379
$global:RedisPassword = "iot_redis_pass123"

# Grafana (if using)
$global:GrafanaHost = "192.168.68.83"     # Update with your K8s Grafana service IP when available
$global:GrafanaPort = 3000

Write-Host "CloudHMI Kubernetes Configuration Loaded" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host "MQTT Broker: $global:MqttBrokerHost:$global:MqttBrokerPort (Anonymous)" -ForegroundColor Cyan
Write-Host "InfluxDB: $global:InfluxDbHost:$global:InfluxDbPort (User: $global:InfluxDbUser)" -ForegroundColor Cyan
Write-Host "Redis: $global:RedisHost:$global:RedisPort (Password: $global:RedisPassword)" -ForegroundColor Cyan
Write-Host "Grafana: $global:GrafanaHost:$global:GrafanaPort" -ForegroundColor Cyan
Write-Host ""
Write-Host "🌐 Web URLs:" -ForegroundColor Yellow
Write-Host "  InfluxDB Admin: http://192.168.68.83:31086" -ForegroundColor White
Write-Host "  InfluxDB HTTPS: https://influxdb.gcelik.dev" -ForegroundColor White
Write-Host "  MQTT HTTPS: https://mqtt.gcelik.dev" -ForegroundColor White
