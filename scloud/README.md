# SmartCloud Industrial IoT Platform

A comprehensive cloud-based Industrial IoT platform for real-time machine monitoring, predictive maintenance, and factory automation.

## 🏗️ System Architecture

### Overview
The SmartCloud platform consists of six main components:

1. **SmartCloud.Core** - Shared models and interfaces
2. **SmartCloud.DataIngestion** - MQTT/OPC UA data collection
3. **SmartCloud.Storage** - InfluxDB time-series storage
4. **SmartCloud.Analytics** - ML.NET predictive maintenance
5. **SmartCloud.Dashboard** - Blazor real-time dashboard
6. **SmartCloud.Gateway** - Main coordinator service

### Architecture Diagram
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Factory I/O   │    │    OpenPLC      │    │   Real PLCs     │
│   (Simulation)  │────│   (Simulation)  │────│   (Production)  │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 │ MQTT/OPC UA
                                 ▼
                    ┌─────────────────────────┐
                    │  SmartCloud.Gateway     │
                    │  - Data Coordination    │
                    │  - Analytics Processing │
                    │  - Alarm Management     │
                    └─────────────┬───────────┘
                                  │
              ┌───────────────────┼───────────────────┐
              │                   │                   │
              ▼                   ▼                   ▼
    ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
    │ DataIngestion   │ │    Storage      │ │   Analytics     │
    │ - MQTT Client   │ │ - InfluxDB      │ │ - ML.NET        │
    │ - OPC UA Client │ │ - SQL Server    │ │ - Anomaly Det.  │
    │ - Data Parsing  │ │ - Time Series   │ │ - Predictions   │
    └─────────────────┘ └─────────────────┘ └─────────────────┘
                                  │
                                  ▼
                        ┌─────────────────┐
                        │   Dashboard     │
                        │ - Blazor UI     │
                        │ - SignalR       │
                        │ - Real-time     │
                        └─────────────────┘
```

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- InfluxDB 2.x
- MQTT Broker (Mosquitto or similar)
- Visual Studio 2022 or VS Code

### 1. Clone and Build
```bash
git clone https://github.com/your-repo/smartcloud
cd smartcloud
dotnet restore
dotnet build
```

### 2. Setup Infrastructure

#### InfluxDB Setup
```bash
# Pull and run InfluxDB
docker run -d -p 8086:8086 influxdb:2.7

# Create organization and bucket via InfluxDB UI (http://localhost:8086)
# Generate API token and update configuration
```

#### MQTT Broker Setup
```bash
# Using Docker
docker run -d -p 1883:1883 eclipse-mosquitto

# Or install locally
# Windows: https://mosquitto.org/download/
# Linux: apt-get install mosquitto mosquitto-clients
```

### 3. Configuration

Update `appsettings.json` in Gateway and Dashboard projects:

```json
{
  "InfluxDB": {
    "Url": "http://localhost:8086",
    "Token": "your-generated-token",
    "Bucket": "smartcloud",
    "Organization": "smartcloud-org"
  },
  "MQTT": {
    "BrokerHost": "localhost",
    "BrokerPort": "1883"
  }
}
```

### 4. Run the Platform

```bash
# Terminal 1 - Start Gateway
cd src/SmartCloud.Gateway
dotnet run

# Terminal 2 - Start Dashboard
cd src/SmartCloud.Dashboard
dotnet run

# Dashboard will be available at: http://localhost:5000
```

## 📊 Data Schema

### InfluxDB Schema

#### PLC Data Measurement
```
Measurement: plcdata
Tags:
- device_id: string (PLC identifier)
- location: string (factory location)
- status: string (Online, Warning, Error, etc.)

Fields:
- temperature: float (°C)
- pressure: float (bar)
- vibration: float (mm/s RMS)
- power_consumption: float (kW)
- cycle_count: integer
- quality: integer (%)
- is_running: boolean
- tags_json: string (custom tags as JSON)
```

#### Maintenance Predictions
```
Measurement: maintenance_predictions
Tags:
- device_id: string
- component_name: string

Fields:
- failure_probability: float (0-1)
- days_to_failure: float
- confidence_score: float (0-1)
- prediction_timestamp: timestamp
```

### MQTT Topic Structure
```
plc/{device_id}/data          # Real-time sensor data
plc/{device_id}/status        # Device status updates
machines/{device_id}/alarms   # Alarm notifications
factory/{location}/status     # Factory-wide status
```

## 🔧 Development Guide

### Adding New Device Types

1. **Create Model** in `SmartCloud.Core/Models/`:
```csharp
public class CustomDeviceData : DeviceDataBase
{
    public double CustomParameter { get; set; }
    // Add device-specific properties
}
```

2. **Update Storage Service** to handle new data type:
```csharp
// Add custom serialization logic in InfluxDbStorageService
private void AddCustomDeviceFields(PointData point, CustomDeviceData data)
{
    point.Field("custom_parameter", data.CustomParameter);
}
```

3. **Update Analytics** for device-specific predictions.

### Adding New Protocols

1. **Implement** `IDataIngestionService` for new protocol
2. **Register** in `Gateway/Program.cs`
3. **Configure** connection parameters

### Extending ML Models

1. **Custom Features**: Modify `MachineDataPoint` class
2. **Training Pipeline**: Update `PredictiveAnalyticsService`
3. **Model Storage**: Implement model versioning

## 🏭 Production Deployment

### Scaling Considerations

1. **Horizontal Scaling**:
   - Multiple Gateway instances with load balancing
   - Separate ingestion and analytics services
   - Redis for shared state

2. **Data Partitioning**:
   - Partition InfluxDB by location/device type
   - Separate databases for hot/cold data

3. **High Availability**:
   - InfluxDB clustering
   - MQTT broker clustering
   - Container orchestration (Kubernetes)

### Security Implementation

#### Industrial Network Security
```csharp
// TLS/SSL for MQTT
var options = new MqttClientOptionsBuilder()
    .WithTcpServer("broker.example.com", 8883)
    .WithTls()
    .WithCredentials("username", "password")
    .Build();
```

#### Authentication & Authorization
```csharp
// JWT Bearer authentication
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "SmartCloud",
            ValidAudience = "SmartCloud.Dashboard",
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });
```

#### Data Encryption
- TLS 1.3 for all communications
- Field-level encryption for sensitive data
- Secure key management (Azure Key Vault)

### Azure Cloud Deployment

#### Infrastructure as Code (ARM Template)
```json
{
  "resources": [
    {
      "type": "Microsoft.ContainerInstance/containerGroups",
      "name": "smartcloud-gateway",
      "properties": {
        "containers": [
          {
            "name": "gateway",
            "properties": {
              "image": "smartcloud/gateway:latest",
              "resources": {
                "requests": {
                  "cpu": 1,
                  "memoryInGb": 2
                }
              }
            }
          }
        ]
      }
    }
  ]
}
```

#### Production Configuration
```json
{
  "InfluxDB": {
    "Url": "https://your-influx-instance.azure.com",
    "Token": "#{InfluxDB.Token}#",
    "Bucket": "production-data"
  },
  "MQTT": {
    "BrokerHost": "production-mqtt.azure.com",
    "BrokerPort": "8883",
    "Username": "#{MQTT.Username}#",
    "Password": "#{MQTT.Password}#"
  }
}
```

## 🧪 Testing Strategy

### Unit Tests
```csharp
[Test]
public async Task PredictMaintenanceAsync_ReturnsValidPrediction()
{
    // Arrange
    var service = new PredictiveAnalyticsService(logger, config, storage);
    
    // Act
    var prediction = await service.PredictMaintenanceAsync("TEST001");
    
    // Assert
    Assert.IsNotNull(prediction);
    Assert.IsTrue(prediction.PredictedFailureProbability >= 0);
    Assert.IsTrue(prediction.PredictedFailureProbability <= 1);
}
```

### Integration Tests
```csharp
[Test]
public async Task DataFlow_FromMqttToStorage_WorksCorrectly()
{
    // Test complete data flow from MQTT to InfluxDB
}
```

### Load Testing
- Simulate thousands of concurrent device connections
- Test data ingestion throughput
- Verify analytics performance under load

## 📈 Monitoring & Observability

### Application Insights Integration
```csharp
services.AddApplicationInsightsTelemetry();
services.AddApplicationInsightsKubernetesEnricher();
```

### Custom Metrics
```csharp
// Track business metrics
telemetryClient.TrackMetric("DevicesConnected", activeDeviceCount);
telemetryClient.TrackMetric("PredictionsGenerated", predictionCount);
telemetryClient.TrackMetric("AnomaliesDetected", anomalyCount);
```

### Health Checks
```csharp
services.AddHealthChecks()
    .AddCheck<InfluxDbHealthCheck>("influxdb")
    .AddCheck<MqttHealthCheck>("mqtt")
    .AddCheck<DiskSpaceHealthCheck>("diskspace");
```

## 🔄 Migration from Simulation to Production

### Phase 1: Simulation
- Factory I/O + OpenPLC simulation
- Local MQTT broker
- Development dashboard

### Phase 2: Hybrid Testing
- Real PLC data alongside simulation
- Production-like infrastructure
- User acceptance testing

### Phase 3: Production Deployment
- Full PLC integration
- Industrial network security
- 24/7 monitoring and support

### Migration Checklist
- [ ] Network security assessment
- [ ] PLC communication protocols verified
- [ ] Data retention policies defined
- [ ] Backup and disaster recovery tested
- [ ] Performance benchmarks established
- [ ] User training completed
- [ ] Support procedures documented

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Implement changes with tests
4. Submit pull request

## 📄 License

MIT License - see LICENSE file for details

## 🆘 Support

- Documentation: [Wiki](link-to-wiki)
- Issues: [GitHub Issues](link-to-issues)
- Discussions: [GitHub Discussions](link-to-discussions)
