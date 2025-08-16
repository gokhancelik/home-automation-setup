using SmartCloud.Core.Interfaces;
using SmartCloud.Core.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace SmartCloud.Gateway.Services;

/// <summary>
/// Main worker service that coordinates data ingestion, storage, and analytics
/// </summary>
public class GatewayWorker : BackgroundService
{
    private readonly ILogger<GatewayWorker> _logger;
    private readonly IDataIngestionService _dataIngestionService;
    private readonly IDataStorageService _storageService;
    private readonly IPredictiveAnalyticsService _analyticsService;
    private readonly IConfiguration _configuration;
    private HubConnection? _dashboardConnection;

    public GatewayWorker(
        ILogger<GatewayWorker> logger,
        IDataIngestionService dataIngestionService,
        IDataStorageService storageService,
        IPredictiveAnalyticsService analyticsService,
        IConfiguration configuration)
    {
        _logger = logger;
        _dataIngestionService = dataIngestionService;
        _storageService = storageService;
        _analyticsService = analyticsService;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SmartCloud Gateway Worker starting");

        try
        {
            // Initialize dashboard connection
            await InitializeDashboardConnection();

            // Subscribe to data ingestion events
            _dataIngestionService.DataReceived += OnDataReceived;

            // Start data ingestion
            await _dataIngestionService.StartAsync(stoppingToken);

            // Start analytics processing (disabled due to InfluxDB 1.x compatibility)
            // _ = Task.Run(() => ProcessAnalytics(stoppingToken), stoppingToken);

            _logger.LogInformation("SmartCloud Gateway Worker started successfully");

            // Keep the worker running
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Gateway Worker");
            throw;
        }
        finally
        {
            await _dataIngestionService.StopAsync(stoppingToken);
            if (_dashboardConnection != null)
            {
                await _dashboardConnection.DisposeAsync();
            }
        }
    }

    private async Task InitializeDashboardConnection()
    {
        try
        {
            var dashboardUrl = _configuration["Dashboard:Url"] ?? "http://localhost:5000";
            _dashboardConnection = new HubConnectionBuilder()
                .WithUrl($"{dashboardUrl}/dashboardhub")
                .WithAutomaticReconnect()
                .Build();

            await _dashboardConnection.StartAsync();
            _logger.LogInformation("Connected to dashboard at {Url}", dashboardUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to dashboard, will continue without real-time updates");
        }
    }

    private async void OnDataReceived(object? sender, DeviceDataReceivedEventArgs e)
    {
        try
        {
            _logger.LogDebug("Received data from device: {DeviceId} via {Protocol}", 
                e.Data.DeviceId, e.Protocol);

            // Store the data
            await _storageService.StoreDeviceDataAsync(e.Data);

            // Send to dashboard if connected
            if (_dashboardConnection?.State == HubConnectionState.Connected && e.Data is PlcData plcData)
            {
                _logger.LogInformation("Sending data to dashboard via SignalR for device: {DeviceId}", plcData.DeviceId);
                await _dashboardConnection.SendAsync("SendDataUpdate", plcData);
            }
            else
            {
                _logger.LogWarning("Dashboard connection not available - State: {State}, Data Type: {DataType}", 
                    _dashboardConnection?.State, e.Data.GetType().Name);
            }

            // Check for anomalies if it's PLC data (disabled due to InfluxDB 1.x compatibility)
            /*if (e.Data is PlcData data)
            {
                await CheckForAnomalies(data);
            }*/
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received data for device: {DeviceId}", e.Data.DeviceId);
        }
    }

    private async Task CheckForAnomalies(PlcData data)
    {
        try
        {
            var anomalyScore = await _analyticsService.CalculateAnomalyScoreAsync(data);
            
            if (anomalyScore > 0.7) // High anomaly threshold
            {
                _logger.LogWarning("High anomaly detected for device {DeviceId}: score {Score:F2}", 
                    data.DeviceId, anomalyScore);

                var alarm = new Alarm
                {
                    Description = $"Anomaly detected on device {data.DeviceId} (score: {anomalyScore:F2})",
                    Severity = anomalyScore > 0.9 ? AlarmSeverity.Critical : AlarmSeverity.Warning,
                    Timestamp = DateTime.UtcNow
                };

                // Send alarm to dashboard
                if (_dashboardConnection?.State == HubConnectionState.Connected)
                {
                    await _dashboardConnection.SendAsync("SendAlarmUpdate", alarm);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking anomalies for device: {DeviceId}", data.DeviceId);
        }
    }

    private async Task ProcessAnalytics(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting analytics processing");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Run predictive maintenance analysis every hour
                await Task.Delay(TimeSpan.FromHours(1), cancellationToken);

                // Get list of active devices (in a real scenario, this would come from a device registry)
                var deviceIds = new[] { "PLC001", "PLC002", "PLC003" };

                foreach (var deviceId in deviceIds)
                {
                    try
                    {
                        var prediction = await _analyticsService.PredictMaintenanceAsync(deviceId, cancellationToken);
                        
                        _logger.LogInformation("Maintenance prediction for {DeviceId}: {Probability:F2} probability", 
                            deviceId, prediction.PredictedFailureProbability);

                        // Send prediction to dashboard
                        if (_dashboardConnection?.State == HubConnectionState.Connected)
                        {
                            await _dashboardConnection.SendAsync("SendMaintenancePrediction", prediction);
                        }

                        // Trigger high-priority alert if failure probability is high
                        if (prediction.PredictedFailureProbability > 0.8)
                        {
                            var alarm = new Alarm
                            {
                                Description = $"High maintenance risk predicted for {deviceId} - {prediction.ComponentName}",
                                Severity = AlarmSeverity.Critical,
                                Timestamp = DateTime.UtcNow
                            };

                            if (_dashboardConnection?.State == HubConnectionState.Connected)
                            {
                                await _dashboardConnection.SendAsync("SendAlarmUpdate", alarm);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing analytics for device: {DeviceId}", deviceId);
                    }
                }

                // Train models weekly
                if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Sunday && DateTime.UtcNow.Hour == 2)
                {
                    await TrainModels(deviceIds, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in analytics processing loop");
            }
        }
    }

    private async Task TrainModels(string[] deviceIds, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting weekly model training");

        foreach (var deviceId in deviceIds)
        {
            try
            {
                await _analyticsService.TrainModelAsync(deviceId, cancellationToken);
                _logger.LogInformation("Model training completed for device: {DeviceId}", deviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error training model for device: {DeviceId}", deviceId);
            }
        }

        _logger.LogInformation("Weekly model training completed");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SmartCloud Gateway Worker stopping");
        
        await _dataIngestionService.StopAsync(cancellationToken);
        
        if (_dashboardConnection != null)
        {
            await _dashboardConnection.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
        _logger.LogInformation("SmartCloud Gateway Worker stopped");
    }
}
