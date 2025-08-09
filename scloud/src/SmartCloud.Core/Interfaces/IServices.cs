namespace SmartCloud.Core.Interfaces;

/// <summary>
/// Interface for data ingestion services
/// </summary>
public interface IDataIngestionService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    event EventHandler<DeviceDataReceivedEventArgs> DataReceived;
}

/// <summary>
/// Interface for data storage services
/// </summary>
public interface IDataStorageService
{
    Task StoreDeviceDataAsync<T>(T data, CancellationToken cancellationToken = default) where T : DeviceDataBase;
    Task<IEnumerable<T>> GetDeviceDataAsync<T>(string deviceId, DateTime from, DateTime to, CancellationToken cancellationToken = default) where T : DeviceDataBase;
    Task<T?> GetLatestDeviceDataAsync<T>(string deviceId, CancellationToken cancellationToken = default) where T : DeviceDataBase;
}

/// <summary>
/// Interface for predictive analytics services
/// </summary>
public interface IPredictiveAnalyticsService
{
    Task<MaintenancePrediction> PredictMaintenanceAsync(string deviceId, CancellationToken cancellationToken = default);
    Task TrainModelAsync(string deviceId, CancellationToken cancellationToken = default);
    Task<double> CalculateAnomalyScoreAsync(PlcData data, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for alarm management
/// </summary>
public interface IAlarmService
{
    Task<IEnumerable<Alarm>> GetActiveAlarmsAsync(string? deviceId = null, CancellationToken cancellationToken = default);
    Task CreateAlarmAsync(Alarm alarm, CancellationToken cancellationToken = default);
    Task AcknowledgeAlarmAsync(string alarmId, string acknowledgedBy, CancellationToken cancellationToken = default);
    Task ResolveAlarmAsync(string alarmId, CancellationToken cancellationToken = default);
    event EventHandler<AlarmCreatedEventArgs> AlarmCreated;
}

/// <summary>
/// Interface for device management
/// </summary>
public interface IDeviceManagementService
{
    Task<IEnumerable<MachineState>> GetAllMachinesAsync(CancellationToken cancellationToken = default);
    Task<MachineState?> GetMachineAsync(string machineId, CancellationToken cancellationToken = default);
    Task UpdateMachineStateAsync(MachineState machine, CancellationToken cancellationToken = default);
    Task RegisterDeviceAsync(string deviceId, string deviceType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Event args for device data received events
/// </summary>
public class DeviceDataReceivedEventArgs : EventArgs
{
    public DeviceDataBase Data { get; }
    public string Protocol { get; }
    
    public DeviceDataReceivedEventArgs(DeviceDataBase data, string protocol)
    {
        Data = data;
        Protocol = protocol;
    }
}

/// <summary>
/// Event args for alarm created events
/// </summary>
public class AlarmCreatedEventArgs : EventArgs
{
    public Alarm Alarm { get; }
    
    public AlarmCreatedEventArgs(Alarm alarm)
    {
        Alarm = alarm;
    }
}
