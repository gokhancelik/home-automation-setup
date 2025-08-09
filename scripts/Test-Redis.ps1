# Redis Test Script for PowerShell
# This script tests the Redis installation

param(
    [string]$RedisHost = "192.168.68.83",
    [int]$RedisPort = 31379,
    [string]$Password = "iot_redis_pass123"
)

Write-Host "Redis Test Results" -ForegroundColor Green
Write-Host "=================" -ForegroundColor Green
Write-Host ""

# Test NodePort connectivity
Write-Host "Testing Redis NodePort connectivity..." -ForegroundColor Yellow
$redisTest = Test-NetConnection -ComputerName $RedisHost -Port $RedisPort -WarningAction SilentlyContinue
if ($redisTest.TcpTestSucceeded) {
    Write-Host "✅ Redis NodePort ($RedisHost`:$RedisPort) is accessible" -ForegroundColor Green
} else {
    Write-Host "❌ Redis NodePort ($RedisHost`:$RedisPort) is not accessible" -ForegroundColor Red
}

Write-Host ""
Write-Host "Available Access Methods:" -ForegroundColor Yellow
Write-Host "1. 🔗 Internal Cluster: redis.redis.svc.cluster.local:6379" -ForegroundColor Cyan
Write-Host "2. 🌐 NodePort: $RedisHost`:31379" -ForegroundColor Cyan
Write-Host "3. 🔧 Port Forward: kubectl port-forward -n redis svc/redis 6379:6379" -ForegroundColor Cyan

Write-Host ""
Write-Host "Authentication:" -ForegroundColor Yellow
Write-Host "  Password: $Password" -ForegroundColor White

Write-Host ""
if (Get-Command redis-cli -ErrorAction SilentlyContinue) {
    Write-Host "Testing with redis-cli..." -ForegroundColor Yellow
    Write-Host "Running: redis-cli -h $RedisHost -p $RedisPort -a $Password ping" -ForegroundColor Yellow
    & redis-cli -h $RedisHost -p $RedisPort -a $Password ping
} else {
    Write-Host "Redis CLI not found. To install:" -ForegroundColor Red
    Write-Host "  - Windows: Download Redis from https://github.com/microsoftarchive/redis/releases" -ForegroundColor Yellow
    Write-Host "  - Or use Docker: docker run --rm -it redis:7.2-alpine redis-cli -h $RedisHost -p $RedisPort -a $Password ping" -ForegroundColor Yellow
    Write-Host "  - Or use kubectl exec: kubectl exec -it deployment/redis -n redis -- redis-cli ping" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Quick Test Commands:" -ForegroundColor Yellow
Write-Host "  Connection Test: redis-cli -h $RedisHost -p $RedisPort -a $Password ping" -ForegroundColor White
Write-Host "  Set Value: redis-cli -h $RedisHost -p $RedisPort -a $Password set test:key 'Hello Redis'" -ForegroundColor White
Write-Host "  Get Value: redis-cli -h $RedisHost -p $RedisPort -a $Password get test:key" -ForegroundColor White
Write-Host "  Info: redis-cli -h $RedisHost -p $RedisPort -a $Password info" -ForegroundColor White

Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Memory Limit: 100MB with LRU eviction" -ForegroundColor White
Write-Host "  Persistence: RDB snapshots + AOF enabled" -ForegroundColor White
Write-Host "  Storage: 2Gi persistent volume" -ForegroundColor White
Write-Host "  Resource Limits: 64Mi-128Mi memory, 50m-200m CPU" -ForegroundColor White
Write-Host "  Security: Password stored as Sealed Secret" -ForegroundColor White
