using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.TimeSeries;
using SmartCloud.Core.Interfaces;
using SmartCloud.Core.Models;

namespace SmartCloud.Analytics.Services;

/// <summary>
/// ML.NET-based predictive analytics service
/// </summary>
public class PredictiveAnalyticsService : IPredictiveAnalyticsService
{
    private readonly ILogger<PredictiveAnalyticsService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDataStorageService _storageService;
    private readonly MLContext _mlContext;
    private readonly Dictionary<string, ITransformer> _trainedModels;

    public PredictiveAnalyticsService(
        ILogger<PredictiveAnalyticsService> logger,
        IConfiguration configuration,
        IDataStorageService storageService)
    {
        _logger = logger;
        _configuration = configuration;
        _storageService = storageService;
        _mlContext = new MLContext(seed: 1);
        _trainedModels = new Dictionary<string, ITransformer>();
    }

    public async Task<MaintenancePrediction> PredictMaintenanceAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get historical data for the last 30 days
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-30);
            
            var historicalData = await _storageService.GetDeviceDataAsync<PlcData>(deviceId, startDate, endDate, cancellationToken);
            var dataList = historicalData.ToList();

            if (!dataList.Any())
            {
                _logger.LogWarning("No historical data found for device: {DeviceId}", deviceId);
                return CreateDefaultPrediction(deviceId);
            }

            // Prepare data for ML model
            var mlData = dataList.Select(d => new MachineDataPoint
            {
                Timestamp = (float)((DateTimeOffset)d.Timestamp).ToUnixTimeSeconds(),
                Temperature = (float)(d.Temperature ?? 0),
                Pressure = (float)(d.Pressure ?? 0),
                Vibration = (float)(d.Vibration ?? 0),
                PowerConsumption = (float)(d.PowerConsumption ?? 0),
                CycleCount = d.CycleCount ?? 0,
                Quality = d.Quality ?? 100
            }).ToArray();

            // Get or train model
            var model = await GetOrTrainModelAsync(deviceId, mlData, cancellationToken);
            
            // Make prediction
            var predictionEngine = _mlContext.Model.CreatePredictionEngine<MachineDataPoint, MaintenancePredictionResult>(model);
            var latestPoint = mlData.Last();
            var prediction = predictionEngine.Predict(latestPoint);

            return new MaintenancePrediction
            {
                DeviceId = deviceId,
                ComponentName = "Main System",
                PredictedFailureProbability = Math.Min(prediction.FailureProbability, 1.0),
                PredictedFailureDate = DateTime.UtcNow.AddDays(prediction.DaysToFailure),
                RecommendedMaintenanceWindow = TimeSpan.FromDays(Math.Max(1, prediction.DaysToFailure - 7)),
                RecommendedActions = GenerateMaintenanceActions(prediction),
                ConfidenceScore = prediction.Confidence,
                PredictionTimestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to predict maintenance for device: {DeviceId}", deviceId);
            return CreateDefaultPrediction(deviceId);
        }
    }

    public async Task TrainModelAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting model training for device: {DeviceId}", deviceId);

            // Get extended historical data for training (90 days)
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-90);
            
            var historicalData = await _storageService.GetDeviceDataAsync<PlcData>(deviceId, startDate, endDate, cancellationToken);
            var dataList = historicalData.ToList();

            if (dataList.Count < 100) // Minimum data points for meaningful training
            {
                _logger.LogWarning("Insufficient data for training model for device: {DeviceId}. Found {Count} data points", 
                    deviceId, dataList.Count);
                return;
            }

            var mlData = dataList.Select(d => new MachineDataPoint
            {
                Timestamp = (float)((DateTimeOffset)d.Timestamp).ToUnixTimeSeconds(),
                Temperature = (float)(d.Temperature ?? 0),
                Pressure = (float)(d.Pressure ?? 0),
                Vibration = (float)(d.Vibration ?? 0),
                PowerConsumption = (float)(d.PowerConsumption ?? 0),
                CycleCount = d.CycleCount ?? 0,
                Quality = d.Quality ?? 100,
                // Label for supervised learning (simplified - in real scenario, you'd have actual failure data)
                Label = CalculateHealthScore(d)
            }).ToArray();

            var dataView = _mlContext.Data.LoadFromEnumerable(mlData);

            // Create training pipeline
            var pipeline = _mlContext.Transforms.Concatenate("Features",
                    nameof(MachineDataPoint.Temperature),
                    nameof(MachineDataPoint.Pressure),
                    nameof(MachineDataPoint.Vibration),
                    nameof(MachineDataPoint.PowerConsumption),
                    nameof(MachineDataPoint.Quality))
                .Append(_mlContext.Transforms.NormalizeMeanVariance("Features"))
                .Append(_mlContext.Regression.Trainers.FastTree());

            // Train the model
            var model = pipeline.Fit(dataView);
            
            // Store the trained model
            _trainedModels[deviceId] = model;
            
            // Optionally save model to disk
            var modelPath = Path.Combine("models", $"{deviceId}_maintenance_model.zip");
            Directory.CreateDirectory("models");
            _mlContext.Model.Save(model, dataView.Schema, modelPath);

            _logger.LogInformation("Model training completed for device: {DeviceId}", deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to train model for device: {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task<double> CalculateAnomalyScoreAsync(PlcData data, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get baseline data for comparison (last 7 days)
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-7);
            
            var baselineData = await _storageService.GetDeviceDataAsync<PlcData>(data.DeviceId, startDate, endDate, cancellationToken);
            var baseline = baselineData.ToList();

            if (!baseline.Any())
            {
                _logger.LogWarning("No baseline data found for anomaly detection for device: {DeviceId}", data.DeviceId);
                return 0.0;
            }

            // Calculate anomaly score based on deviation from baseline
            var anomalyScore = 0.0;
            var factorCount = 0;

            // Temperature anomaly
            if (data.Temperature.HasValue && baseline.Any(b => b.Temperature.HasValue))
            {
                var avgTemp = baseline.Where(b => b.Temperature.HasValue).Average(b => b.Temperature!.Value);
                var tempDeviation = Math.Abs(data.Temperature.Value - avgTemp) / avgTemp;
                anomalyScore += Math.Min(tempDeviation, 1.0);
                factorCount++;
            }

            // Pressure anomaly
            if (data.Pressure.HasValue && baseline.Any(b => b.Pressure.HasValue))
            {
                var avgPressure = baseline.Where(b => b.Pressure.HasValue).Average(b => b.Pressure!.Value);
                var pressureDeviation = Math.Abs(data.Pressure.Value - avgPressure) / avgPressure;
                anomalyScore += Math.Min(pressureDeviation, 1.0);
                factorCount++;
            }

            // Vibration anomaly
            if (data.Vibration.HasValue && baseline.Any(b => b.Vibration.HasValue))
            {
                var avgVibration = baseline.Where(b => b.Vibration.HasValue).Average(b => b.Vibration!.Value);
                var vibrationDeviation = Math.Abs(data.Vibration.Value - avgVibration) / (avgVibration + 0.001); // Avoid division by zero
                anomalyScore += Math.Min(vibrationDeviation, 1.0);
                factorCount++;
            }

            // Power consumption anomaly
            if (data.PowerConsumption.HasValue && baseline.Any(b => b.PowerConsumption.HasValue))
            {
                var avgPower = baseline.Where(b => b.PowerConsumption.HasValue).Average(b => b.PowerConsumption!.Value);
                var powerDeviation = Math.Abs(data.PowerConsumption.Value - avgPower) / avgPower;
                anomalyScore += Math.Min(powerDeviation, 1.0);
                factorCount++;
            }

            return factorCount > 0 ? anomalyScore / factorCount : 0.0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate anomaly score for device: {DeviceId}", data.DeviceId);
            return 0.0;
        }
    }

    private async Task<ITransformer> GetOrTrainModelAsync(string deviceId, MachineDataPoint[] data, CancellationToken cancellationToken)
    {
        // Check if we have a cached model
        if (_trainedModels.TryGetValue(deviceId, out var cachedModel))
        {
            return cachedModel;
        }

        // Check if we have a saved model on disk
        var modelPath = Path.Combine("models", $"{deviceId}_maintenance_model.zip");
        if (File.Exists(modelPath))
        {
            try
            {
                var loadedModel = _mlContext.Model.Load(modelPath, out var schema);
                _trainedModels[deviceId] = loadedModel;
                return loadedModel;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load saved model for device: {DeviceId}, will retrain", deviceId);
            }
        }

        // Train a new model
        await TrainModelAsync(deviceId, cancellationToken);
        
        return _trainedModels.TryGetValue(deviceId, out var newModel) 
            ? newModel 
            : CreateDefaultModel();
    }

    private ITransformer CreateDefaultModel()
    {
        // Create a simple default model for fallback
        var emptyData = new MachineDataPoint[0];
        var dataView = _mlContext.Data.LoadFromEnumerable(emptyData);
        
        var pipeline = _mlContext.Transforms.Concatenate("Features", nameof(MachineDataPoint.Temperature))
            .Append(_mlContext.Transforms.NormalizeMeanVariance("Features"))
            .Append(_mlContext.Regression.Trainers.FastTree());

        return pipeline.Fit(dataView);
    }

    private float CalculateHealthScore(PlcData data)
    {
        // Simplified health score calculation
        // In a real scenario, this would be based on actual maintenance records and failure data
        var score = 100.0f;

        if (data.Temperature.HasValue && data.Temperature > 80) // Example threshold
            score -= 10;

        if (data.Vibration.HasValue && data.Vibration > 5) // Example threshold
            score -= 15;

        if (data.Quality.HasValue && data.Quality < 90)
            score -= 5;

        if (!data.IsRunning)
            score -= 20;

        return Math.Max(0, score) / 100.0f;
    }

    private MaintenancePrediction CreateDefaultPrediction(string deviceId)
    {
        return new MaintenancePrediction
        {
            DeviceId = deviceId,
            ComponentName = "Main System",
            PredictedFailureProbability = 0.1,
            PredictedFailureDate = DateTime.UtcNow.AddDays(30),
            RecommendedMaintenanceWindow = TimeSpan.FromDays(7),
            RecommendedActions = new[] { "Perform routine inspection", "Check sensor calibration" },
            ConfidenceScore = 0.5,
            PredictionTimestamp = DateTime.UtcNow
        };
    }

    private string[] GenerateMaintenanceActions(MaintenancePredictionResult prediction)
    {
        var actions = new List<string>();

        if (prediction.FailureProbability > 0.7)
        {
            actions.Add("Schedule immediate maintenance");
            actions.Add("Inspect critical components");
        }
        else if (prediction.FailureProbability > 0.4)
        {
            actions.Add("Schedule preventive maintenance");
            actions.Add("Monitor closely");
        }
        else
        {
            actions.Add("Continue normal operation");
            actions.Add("Routine inspection recommended");
        }

        return actions.ToArray();
    }
}

/// <summary>
/// Data point for ML training
/// </summary>
public class MachineDataPoint
{
    public float Timestamp { get; set; }
    public float Temperature { get; set; }
    public float Pressure { get; set; }
    public float Vibration { get; set; }
    public float PowerConsumption { get; set; }
    public int CycleCount { get; set; }
    public int Quality { get; set; }
    public float Label { get; set; } // Health score for supervised learning
}

/// <summary>
/// ML prediction result
/// </summary>
public class MaintenancePredictionResult
{
    [ColumnName("Score")]
    public float FailureProbability { get; set; }
    
    public float DaysToFailure => Math.Max(1, (1 - FailureProbability) * 30);
    public float Confidence => Math.Min(1.0f, Math.Max(0.5f, 1 - Math.Abs(FailureProbability - 0.5f) * 2));
}
