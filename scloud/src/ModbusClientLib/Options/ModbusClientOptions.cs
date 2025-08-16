using ModbusClientLib.Codec;

namespace ModbusClientLib.Options;

/// <summary>
/// Configuration options for the Modbus TCP client
/// </summary>
public class ModbusClientOptions
{
    /// <summary>
    /// Host address of the Modbus TCP server
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Port number of the Modbus TCP server
    /// </summary>
    public int Port { get; set; } = 502;

    /// <summary>
    /// Slave ID (Unit ID) for Modbus communication
    /// </summary>
    public byte SlaveId { get; set; } = 1;

    /// <summary>
    /// Timeout for establishing TCP connection
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Timeout for individual Modbus requests
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum number of retries for failed operations
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Base delay between reconnection attempts
    /// </summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Default endianness for data conversion
    /// </summary>
    public ModbusEndianness Endianness { get; set; } = ModbusEndianness.BigEndian;

    /// <summary>
    /// Default word order for 32-bit data conversion
    /// </summary>
    public WordOrder WordOrder { get; set; } = WordOrder.ABCD;

    /// <summary>
    /// Enable automatic reconnection on connection loss
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Maximum jitter percentage for retry delays (0.0 to 1.0)
    /// </summary>
    public double RetryJitterFactor { get; set; } = 0.1;

    /// <summary>
    /// Maximum delay for exponential backoff
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to use exponential backoff for retries
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Client identifier for logging purposes
    /// </summary>
    public string ClientId { get; set; } = Environment.MachineName;

    /// <summary>
    /// Validates the configuration options
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when configuration is invalid</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new ArgumentException("Host cannot be null or empty", nameof(Host));

        if (Port < 1 || Port > 65535)
            throw new ArgumentException("Port must be between 1 and 65535", nameof(Port));

        if (ConnectTimeout <= TimeSpan.Zero)
            throw new ArgumentException("Connect timeout must be positive", nameof(ConnectTimeout));

        if (RequestTimeout <= TimeSpan.Zero)
            throw new ArgumentException("Request timeout must be positive", nameof(RequestTimeout));

        if (MaxRetries < 0)
            throw new ArgumentException("Max retries cannot be negative", nameof(MaxRetries));

        if (ReconnectDelay < TimeSpan.Zero)
            throw new ArgumentException("Reconnect delay cannot be negative", nameof(ReconnectDelay));

        if (RetryJitterFactor < 0.0 || RetryJitterFactor > 1.0)
            throw new ArgumentException("Retry jitter factor must be between 0.0 and 1.0", nameof(RetryJitterFactor));

        if (MaxRetryDelay <= TimeSpan.Zero)
            throw new ArgumentException("Max retry delay must be positive", nameof(MaxRetryDelay));

        if (string.IsNullOrWhiteSpace(ClientId))
            throw new ArgumentException("Client ID cannot be null or empty", nameof(ClientId));
    }
}