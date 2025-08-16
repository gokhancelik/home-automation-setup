using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MQTTnet;
using SmartCloud.Core.Interfaces;
using SmartCloud.Core.Models;
using System.Text.Json;

namespace SmartCloud.DataIngestion.Services;

/// <summary>
/// MQTT data ingestion service for receiving PLC data
/// </summary>
public class MqttDataIngestionService : IDataIngestionService, IDisposable
{
    private readonly ILogger<MqttDataIngestionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IMqttClient _mqttClient;
    private bool _isRunning;
    private bool _disposed;

    public event EventHandler<DeviceDataReceivedEventArgs>? DataReceived;

    public MqttDataIngestionService(ILogger<MqttDataIngestionService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();
        
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;
        _mqttClient.ConnectedAsync += OnConnected;
        _mqttClient.DisconnectedAsync += OnDisconnected;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("MQTT service is already running");
            return;
        }

        try
        {
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_configuration["MQTT:BrokerHost"] ?? "localhost", 
                             int.Parse(_configuration["MQTT:BrokerPort"] ?? "1883"))
                .WithCredentials(_configuration["MQTT:Username"], _configuration["MQTT:Password"])
                .WithClientId($"SmartCloud_{Environment.MachineName}_{Guid.NewGuid():N}"[..23])
                .WithCleanSession()
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
                .Build();

            await _mqttClient.ConnectAsync(options, cancellationToken);
            
            // Subscribe to PLC data topics
            var subscriptions = new[]
            {
                "plc/+/data",      // PLC data from any device
                "factory/+/status", // Factory status updates
                "machines/+/alarms" // Machine alarms
            };

            foreach (var topic in subscriptions)
            {
                await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder()
                    .WithTopic(topic)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build(), cancellationToken);
                
                _logger.LogInformation("Subscribed to MQTT topic: {Topic}", topic);
            }

            _isRunning = true;
            _logger.LogInformation("MQTT Data Ingestion Service started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MQTT Data Ingestion Service");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return;
        }

        try
        {
            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync(cancellationToken: cancellationToken);
            }
            
            _isRunning = false;
            _logger.LogInformation("MQTT Data Ingestion Service stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping MQTT Data Ingestion Service");
        }
    }

    private async Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.ConvertPayloadToString();
            
            _logger.LogInformation("MQTT message received on topic: {Topic} with payload length: {Length}", topic, payload.Length);

            var data = await ParseMessageAsync(topic, payload);
            if (data != null)
            {
                _logger.LogInformation("Successfully parsed data for device: {DeviceId}", data.DeviceId);
                DataReceived?.Invoke(this, new DeviceDataReceivedEventArgs(data, "MQTT"));
            }
            else
            {
                _logger.LogWarning("Failed to parse data from topic: {Topic}", topic);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message on topic: {Topic}", e.ApplicationMessage.Topic);
        }
    }

    private async Task<DeviceDataBase?> ParseMessageAsync(string topic, string payload)
    {
        try
        {
            var topicParts = topic.Split('/');
            
            _logger.LogInformation("Parsing message - Topic: {Topic}, Parts: [{Parts}], Payload: {Payload}", 
                topic, string.Join(", ", topicParts), payload);
            
            if (topicParts.Length < 3)
            {
                _logger.LogWarning("Invalid topic format: {Topic} - Expected format: type/deviceId/messageType", topic);
                return null;
            }

            var deviceId = topicParts[1];
            var messageType = topicParts[2];

            _logger.LogInformation("Processing message type '{MessageType}' for device '{DeviceId}'", messageType, deviceId);

            switch (messageType.ToLowerInvariant())
            {
                case "data":
                    return await ParsePlcDataAsync(deviceId, payload);
                
                case "status":
                    return await ParseStatusDataAsync(deviceId, payload);
                
                case "alarms":
                    return await ParseAlarmDataAsync(deviceId, payload);
                
                default:
                    _logger.LogWarning("Unknown message type: {MessageType} for device: {DeviceId}", messageType, deviceId);
                    return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse message payload for topic: {Topic}", topic);
            return null;
        }
    }

    private async Task<PlcData?> ParsePlcDataAsync(string deviceId, string payload)
    {
        try
        {
            var jsonData = JsonSerializer.Deserialize<Dictionary<string, object>>(payload);
            if (jsonData == null) return null;

            var plcData = new PlcData
            {
                DeviceId = deviceId,
                Timestamp = DateTime.UtcNow,
                Tags = jsonData
            };

            // Extract common PLC parameters
            if (jsonData.TryGetValue("temperature", out var temp) && double.TryParse(temp.ToString(), out var temperature))
                plcData.Temperature = temperature;

            if (jsonData.TryGetValue("pressure", out var press) && double.TryParse(press.ToString(), out var pressure))
                plcData.Pressure = pressure;

            if (jsonData.TryGetValue("vibration", out var vib) && double.TryParse(vib.ToString(), out var vibration))
                plcData.Vibration = vibration;

            if (jsonData.TryGetValue("cycle_count", out var cycle) && int.TryParse(cycle.ToString(), out var cycleCount))
                plcData.CycleCount = cycleCount;

            if (jsonData.TryGetValue("is_running", out var running) && bool.TryParse(running.ToString(), out var isRunning))
                plcData.IsRunning = isRunning;

            if (jsonData.TryGetValue("power_consumption", out var power) && double.TryParse(power.ToString(), out var powerConsumption))
                plcData.PowerConsumption = powerConsumption;

            if (jsonData.TryGetValue("quality", out var qual) && int.TryParse(qual.ToString(), out var quality))
                plcData.Quality = quality;

            if (jsonData.TryGetValue("location", out var loc))
                plcData.Location = loc.ToString() ?? string.Empty;

            return plcData;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse PLC data JSON for device: {DeviceId}", deviceId);
            return null;
        }
    }

    private async Task<DeviceDataBase?> ParseStatusDataAsync(string deviceId, string payload)
    {
        // Implementation for status data parsing
        // This can be extended based on specific status message formats
        return await Task.FromResult<DeviceDataBase?>(null);
    }

    private async Task<DeviceDataBase?> ParseAlarmDataAsync(string deviceId, string payload)
    {
        // Implementation for alarm data parsing
        // This can be extended based on specific alarm message formats
        return await Task.FromResult<DeviceDataBase?>(null);
    }

    private async Task OnConnected(MqttClientConnectedEventArgs e)
    {
        _logger.LogInformation("Connected to MQTT broker successfully");
        await Task.CompletedTask;
    }

    private async Task OnDisconnected(MqttClientDisconnectedEventArgs e)
    {
        _logger.LogWarning("Disconnected from MQTT broker. Reason: {Reason}", e.Reason);
        
        if (_isRunning && !_disposed)
        {
            _logger.LogInformation("Attempting to reconnect to MQTT broker in 5 seconds...");
            await Task.Delay(5000);
            
            try
            {
                await StartAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect to MQTT broker");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        StopAsync().GetAwaiter().GetResult();
        _mqttClient?.Dispose();
    }
}
