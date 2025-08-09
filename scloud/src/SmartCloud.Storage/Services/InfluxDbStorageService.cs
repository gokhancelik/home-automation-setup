using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartCloud.Core.Interfaces;
using SmartCloud.Core.Models;
using System.Text.Json;

namespace SmartCloud.Storage.Services;

/// <summary>
/// InfluxDB-based time series data storage service
/// </summary>
public class InfluxDbStorageService : IDataStorageService, IDisposable
{
    private readonly ILogger<InfluxDbStorageService> _logger;
    private readonly IConfiguration _configuration;
    private readonly InfluxDBClient _influxClient;
    private readonly string _bucket;
    private readonly string _organization;
    private bool _disposed;

    public InfluxDbStorageService(ILogger<InfluxDbStorageService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        var url = _configuration["InfluxDB:Url"] ?? "http://localhost:8086";
        var token = _configuration["InfluxDB:Token"] ?? throw new ArgumentException("InfluxDB token is required");
        _bucket = _configuration["InfluxDB:Bucket"] ?? "smartcloud";
        _organization = _configuration["InfluxDB:Organization"] ?? "smartcloud-org";

        _influxClient = new InfluxDBClient(url, token);
        
        _logger.LogInformation("InfluxDB client initialized for bucket: {Bucket}, organization: {Organization}", _bucket, _organization);
    }

    public async Task StoreDeviceDataAsync<T>(T data, CancellationToken cancellationToken = default) where T : DeviceDataBase
    {
        try
        {
            var writeApi = _influxClient.GetWriteApiAsync();
            
            var point = CreateDataPoint(data);
            await writeApi.WritePointAsync(point, _bucket, _organization, cancellationToken);
            
            _logger.LogDebug("Stored data point for device: {DeviceId} at {Timestamp}", data.DeviceId, data.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store device data for device: {DeviceId}", data.DeviceId);
            throw;
        }
    }

    public async Task<IEnumerable<T>> GetDeviceDataAsync<T>(string deviceId, DateTime from, DateTime to, CancellationToken cancellationToken = default) where T : DeviceDataBase
    {
        try
        {
            var measurement = GetMeasurementName<T>();
            var query = $@"
                from(bucket: ""{_bucket}"")
                |> range(start: {from:yyyy-MM-ddTHH:mm:ssZ}, stop: {to:yyyy-MM-ddTHH:mm:ssZ})
                |> filter(fn: (r) => r._measurement == ""{measurement}"")
                |> filter(fn: (r) => r.device_id == ""{deviceId}"")
                |> pivot(rowKey:[""_time""], columnKey: [""_field""], valueColumn: ""_value"")";

            var queryApi = _influxClient.GetQueryApi();
            var tables = await queryApi.QueryAsync(query, _organization, cancellationToken);
            
            var results = new List<T>();
            
            foreach (var table in tables)
            {
                foreach (var record in table.Records)
                {
                    var deviceData = DeserializeRecord<T>(record);
                    if (deviceData != null)
                    {
                        results.Add(deviceData);
                    }
                }
            }
            
            _logger.LogDebug("Retrieved {Count} records for device: {DeviceId} between {From} and {To}", 
                results.Count, deviceId, from, to);
            
            return results.OrderBy(x => x.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve device data for device: {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task<T?> GetLatestDeviceDataAsync<T>(string deviceId, CancellationToken cancellationToken = default) where T : DeviceDataBase
    {
        try
        {
            var measurement = GetMeasurementName<T>();
            var query = $@"
                from(bucket: ""{_bucket}"")
                |> range(start: -24h)
                |> filter(fn: (r) => r._measurement == ""{measurement}"")
                |> filter(fn: (r) => r.device_id == ""{deviceId}"")
                |> last()
                |> pivot(rowKey:[""_time""], columnKey: [""_field""], valueColumn: ""_value"")";

            var queryApi = _influxClient.GetQueryApi();
            var tables = await queryApi.QueryAsync(query, _organization, cancellationToken);
            
            foreach (var table in tables)
            {
                foreach (var record in table.Records)
                {
                    var deviceData = DeserializeRecord<T>(record);
                    if (deviceData != null)
                    {
                        _logger.LogDebug("Retrieved latest data for device: {DeviceId} at {Timestamp}", 
                            deviceId, deviceData.Timestamp);
                        return deviceData;
                    }
                }
            }
            
            _logger.LogDebug("No recent data found for device: {DeviceId}", deviceId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve latest device data for device: {DeviceId}", deviceId);
            throw;
        }
    }

    private PointData CreateDataPoint<T>(T data) where T : DeviceDataBase
    {
        var measurement = GetMeasurementName<T>();
        var point = PointData
            .Measurement(measurement)
            .Tag("device_id", data.DeviceId)
            .Tag("location", data.Location)
            .Tag("status", data.Status.ToString())
            .Timestamp(data.Timestamp, WritePrecision.Ms);

        // Add type-specific fields
        switch (data)
        {
            case PlcData plcData:
                AddPlcDataFields(point, plcData);
                break;
            default:
                // Add base fields using reflection for extensibility
                AddGenericFields(point, data);
                break;
        }

        return point;
    }

    private void AddPlcDataFields(PointData point, PlcData plcData)
    {
        if (plcData.Temperature.HasValue)
            point.Field("temperature", plcData.Temperature.Value);
        
        if (plcData.Pressure.HasValue)
            point.Field("pressure", plcData.Pressure.Value);
        
        if (plcData.Vibration.HasValue)
            point.Field("vibration", plcData.Vibration.Value);
        
        if (plcData.CycleCount.HasValue)
            point.Field("cycle_count", plcData.CycleCount.Value);
        
        point.Field("is_running", plcData.IsRunning);
        
        if (!string.IsNullOrEmpty(plcData.AlarmStatus))
            point.Tag("alarm_status", plcData.AlarmStatus);
        
        if (plcData.PowerConsumption.HasValue)
            point.Field("power_consumption", plcData.PowerConsumption.Value);
        
        if (plcData.Quality.HasValue)
            point.Field("quality", plcData.Quality.Value);

        // Add custom tags as JSON field for flexibility
        if (plcData.Tags.Any())
        {
            var tagsJson = JsonSerializer.Serialize(plcData.Tags);
            point.Field("tags_json", tagsJson);
        }
    }

    private void AddGenericFields(PointData point, DeviceDataBase data)
    {
        // Use reflection to add fields from unknown types
        var properties = data.GetType().GetProperties()
            .Where(p => p.CanRead && 
                       p.Name != nameof(DeviceDataBase.DeviceId) && 
                       p.Name != nameof(DeviceDataBase.Timestamp) && 
                       p.Name != nameof(DeviceDataBase.Location) && 
                       p.Name != nameof(DeviceDataBase.Status));

        foreach (var property in properties)
        {
            var value = property.GetValue(data);
            if (value != null)
            {
                switch (value)
                {
                    case double d:
                        point.Field(property.Name.ToLowerInvariant(), d);
                        break;
                    case float f:
                        point.Field(property.Name.ToLowerInvariant(), f);
                        break;
                    case int i:
                        point.Field(property.Name.ToLowerInvariant(), i);
                        break;
                    case long l:
                        point.Field(property.Name.ToLowerInvariant(), l);
                        break;
                    case bool b:
                        point.Field(property.Name.ToLowerInvariant(), b);
                        break;
                    case string s:
                        point.Field(property.Name.ToLowerInvariant(), s);
                        break;
                    default:
                        // Serialize complex objects as JSON
                        var json = JsonSerializer.Serialize(value);
                        point.Field(property.Name.ToLowerInvariant(), json);
                        break;
                }
            }
        }
    }

    private T? DeserializeRecord<T>(InfluxDB.Client.Core.Flux.Domain.FluxRecord record) where T : DeviceDataBase
    {
        try
        {
            // This is a simplified deserialization - in a real implementation,
            // you would need to properly map InfluxDB records back to your models
            var json = JsonSerializer.Serialize(record.Values);
            var deviceData = JsonSerializer.Deserialize<T>(json);
            
            return deviceData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize InfluxDB record");
            return null;
        }
    }

    private static string GetMeasurementName<T>() where T : DeviceDataBase
    {
        return typeof(T).Name.ToLowerInvariant();
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _influxClient?.Dispose();
        _logger.LogInformation("InfluxDB client disposed");
    }
}
