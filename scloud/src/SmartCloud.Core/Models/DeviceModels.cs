namespace SmartCloud.Core.Models;

/// <summary>
/// Base model for all IoT device data
/// </summary>
public abstract class DeviceDataBase
{
    public string DeviceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Location { get; set; } = string.Empty;
    public DeviceStatus Status { get; set; } = DeviceStatus.Online;
}

/// <summary>
/// PLC data model for machine telemetry
/// </summary>
public class PlcData : DeviceDataBase
{
    public Dictionary<string, object> Tags { get; set; } = new();
    public double? Temperature { get; set; }
    public double? Pressure { get; set; }
    public double? Vibration { get; set; }
    public int? CycleCount { get; set; }
    public bool IsRunning { get; set; }
    public string? AlarmStatus { get; set; }
    public double? PowerConsumption { get; set; }
    public int? Quality { get; set; }
}

/// <summary>
/// Machine state information
/// </summary>
public class MachineState
{
    public string MachineId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public MachineStatus Status { get; set; }
    public DateTime LastUpdate { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public List<Alarm> ActiveAlarms { get; set; } = new();
}

/// <summary>
/// Alarm information
/// </summary>
public class Alarm
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Description { get; set; } = string.Empty;
    public AlarmSeverity Severity { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsAcknowledged { get; set; }
    public string? AcknowledgedBy { get; set; }
}

/// <summary>
/// Predictive maintenance prediction
/// </summary>
public class MaintenancePrediction
{
    public string DeviceId { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public double PredictedFailureProbability { get; set; }
    public DateTime PredictedFailureDate { get; set; }
    public TimeSpan RecommendedMaintenanceWindow { get; set; }
    public string[] RecommendedActions { get; set; } = Array.Empty<string>();
    public double ConfidenceScore { get; set; }
    public DateTime PredictionTimestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Device status enumeration
/// </summary>
public enum DeviceStatus
{
    Online,
    Offline,
    Warning,
    Error,
    Maintenance
}

/// <summary>
/// Machine status enumeration
/// </summary>
public enum MachineStatus
{
    Idle,
    Running,
    Stopped,
    Maintenance,
    Error,
    Setup
}

/// <summary>
/// Alarm severity levels
/// </summary>
public enum AlarmSeverity
{
    Info,
    Warning,
    Critical,
    Emergency
}
