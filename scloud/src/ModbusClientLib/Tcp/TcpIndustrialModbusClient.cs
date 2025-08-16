using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModbusClientLib.Abstractions;
using ModbusClientLib.Options;
using NModbus;
using System.Net;
using System.Net.Sockets;

namespace ModbusClientLib.Tcp;

/// <summary>
/// Production-ready Modbus TCP client implementation with retry logic, reconnection, and comprehensive logging
/// </summary>
public class TcpIndustrialModbusClient : IIndustrialModbusClient
{
    private readonly ILogger<TcpIndustrialModbusClient> _logger;
    private readonly ModbusClientOptions _options;
    private readonly Random _random = new();
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private readonly SemaphoreSlim _operationSemaphore = new(1, 1);

    private TcpClient? _tcpClient;
    private IModbusMaster? _modbusMaster;
    private bool _disposed;
    private CancellationTokenSource? _reconnectionCts;

    /// <summary>
    /// Initializes a new instance of the TcpIndustrialModbusClient class
    /// </summary>
    /// <param name="options">Client configuration options</param>
    /// <param name="logger">Logger instance</param>
    public TcpIndustrialModbusClient(IOptions<ModbusClientOptions> options, ILogger<TcpIndustrialModbusClient> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _options.Validate();
        
        _logger.LogInformation("Modbus TCP client initialized for {Host}:{Port}, SlaveId: {SlaveId}, ClientId: {ClientId}",
            _options.Host, _options.Port, _options.SlaveId, _options.ClientId);
    }

    /// <inheritdoc />
    public bool IsConnected => _tcpClient?.Connected == true && _modbusMaster != null;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_disposed) 
            throw new ObjectDisposedException(nameof(TcpIndustrialModbusClient));

        await _connectionSemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsConnected)
            {
                _logger.LogDebug("Already connected to {Host}:{Port}", _options.Host, _options.Port);
                return;
            }

            _logger.LogInformation("Connecting to Modbus TCP server at {Host}:{Port}...", _options.Host, _options.Port);

            await DisconnectInternalAsync().ConfigureAwait(false);

            var connectStartTime = DateTime.UtcNow;
            
            try
            {
                _tcpClient = new TcpClient();
                
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connectCts.CancelAfter(_options.ConnectTimeout);

                await _tcpClient.ConnectAsync(IPAddress.Parse(_options.Host), _options.Port, connectCts.Token).ConfigureAwait(false);
                
                // Configure TCP client for better performance
                _tcpClient.ReceiveTimeout = (int)_options.RequestTimeout.TotalMilliseconds;
                _tcpClient.SendTimeout = (int)_options.RequestTimeout.TotalMilliseconds;
                _tcpClient.NoDelay = true;

                var factory = new ModbusFactory();
                _modbusMaster = factory.CreateMaster(_tcpClient);

                var connectDuration = DateTime.UtcNow - connectStartTime;
                _logger.LogInformation("Successfully connected to {Host}:{Port} in {Duration}ms",
                    _options.Host, _options.Port, connectDuration.TotalMilliseconds);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                await DisconnectInternalAsync().ConfigureAwait(false);
                throw;
            }
            catch (OperationCanceledException)
            {
                await DisconnectInternalAsync().ConfigureAwait(false);
                var message = $"Connection to {_options.Host}:{_options.Port} timed out after {_options.ConnectTimeout}";
                _logger.LogError(message);
                throw new ModbusConnectionException(message);
            }
            catch (Exception ex)
            {
                await DisconnectInternalAsync().ConfigureAwait(false);
                var message = $"Failed to connect to {_options.Host}:{_options.Port}";
                _logger.LogError(ex, message);
                throw new ModbusConnectionException(message, ex);
            }
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_disposed) 
            throw new ObjectDisposedException(nameof(TcpIndustrialModbusClient));

        await _connectionSemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await DisconnectInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort count, CancellationToken ct = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Reading {Count} coils starting at address {Address}", count, startAddress);
            var result = await _modbusMaster!.ReadCoilsAsync(_options.SlaveId, startAddress, count).ConfigureAwait(false);
            _logger.LogDebug("Successfully read {Count} coils from address {Address}", count, startAddress);
            return result;
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool[]> ReadDiscreteInputsAsync(ushort startAddress, ushort count, CancellationToken ct = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Reading {Count} discrete inputs starting at address {Address}", count, startAddress);
            var result = await _modbusMaster!.ReadInputsAsync(_options.SlaveId, startAddress, count).ConfigureAwait(false);
            _logger.LogDebug("Successfully read {Count} discrete inputs from address {Address}", count, startAddress);
            return result;
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort count, CancellationToken ct = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Reading {Count} holding registers starting at address {Address}", count, startAddress);
            var result = await _modbusMaster!.ReadHoldingRegistersAsync(_options.SlaveId, startAddress, count).ConfigureAwait(false);
            _logger.LogDebug("Successfully read {Count} holding registers from address {Address}", count, startAddress);
            return result;
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ushort[]> ReadInputRegistersAsync(ushort startAddress, ushort count, CancellationToken ct = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Reading {Count} input registers starting at address {Address}", count, startAddress);
            var result = await _modbusMaster!.ReadInputRegistersAsync(_options.SlaveId, startAddress, count).ConfigureAwait(false);
            _logger.LogDebug("Successfully read {Count} input registers from address {Address}", count, startAddress);
            return result;
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteSingleCoilAsync(ushort address, bool value, CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Writing coil at address {Address} to {Value}", address, value);
            await _modbusMaster!.WriteSingleCoilAsync(_options.SlaveId, address, value).ConfigureAwait(false);
            _logger.LogDebug("Successfully wrote coil at address {Address} to {Value}", address, value);
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteSingleRegisterAsync(ushort address, ushort value, CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Writing register at address {Address} to {Value}", address, value);
            await _modbusMaster!.WriteSingleRegisterAsync(_options.SlaveId, address, value).ConfigureAwait(false);
            _logger.LogDebug("Successfully wrote register at address {Address} to {Value}", address, value);
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Writing {Count} registers starting at address {Address}", values.Length, startAddress);
            await _modbusMaster!.WriteMultipleRegistersAsync(_options.SlaveId, startAddress, values).ConfigureAwait(false);
            _logger.LogDebug("Successfully wrote {Count} registers starting at address {Address}", values.Length, startAddress);
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteMultipleCoilsAsync(ushort startAddress, bool[] values, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);

        await ExecuteWithRetryAsync(async () =>
        {
            _logger.LogDebug("Writing {Count} coils starting at address {Address}", values.Length, startAddress);
            await _modbusMaster!.WriteMultipleCoilsAsync(_options.SlaveId, startAddress, values).ConfigureAwait(false);
            _logger.LogDebug("Successfully wrote {Count} coils starting at address {Address}", values.Length, startAddress);
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes an operation with retry logic and automatic reconnection
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        if (_disposed) 
            throw new ObjectDisposedException(nameof(TcpIndustrialModbusClient));

        await _operationSemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Exception? lastException = null;
            var attempt = 0;

            while (attempt <= _options.MaxRetries)
            {
                try
                {
                    if (!IsConnected)
                    {
                        _logger.LogWarning("Not connected, attempting to connect before operation (attempt {Attempt})", attempt + 1);
                        await ConnectAsync(ct).ConfigureAwait(false);
                    }

                    var operationStartTime = DateTime.UtcNow;
                    using var operationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    operationCts.CancelAfter(_options.RequestTimeout);

                    var result = await operation().ConfigureAwait(false);
                    
                    var operationDuration = DateTime.UtcNow - operationStartTime;
                    if (operationDuration > TimeSpan.FromMilliseconds(100)) // Log slow operations
                    {
                        _logger.LogWarning("Slow Modbus operation completed in {Duration}ms", operationDuration.TotalMilliseconds);
                    }

                    return result;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw; // Don't retry cancellation
                }
                catch (OperationCanceledException)
                {
                    lastException = new ModbusTimeoutException($"Operation timed out after {_options.RequestTimeout}", _options.RequestTimeout);
                }
                catch (Exception ex) when (IsConnectionException(ex))
                {
                    lastException = ex;
                    await HandleConnectionLossAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }

                attempt++;
                if (attempt <= _options.MaxRetries)
                {
                    var delay = CalculateRetryDelay(attempt);
                    _logger.LogWarning(lastException, "Operation failed (attempt {Attempt}/{MaxAttempts}), retrying in {Delay}ms",
                        attempt, _options.MaxRetries + 1, delay.TotalMilliseconds);

                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }

            _logger.LogError(lastException, "Operation failed after {Attempts} attempts", _options.MaxRetries + 1);
            throw lastException ?? new ModbusException("Operation failed after maximum retries");
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    /// <summary>
    /// Executes an operation with retry logic (void return)
    /// </summary>
    private async Task ExecuteWithRetryAsync(Func<Task> operation, CancellationToken ct)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation().ConfigureAwait(false);
            return true; // Dummy return value
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Calculates retry delay with exponential backoff and jitter
    /// </summary>
    private TimeSpan CalculateRetryDelay(int attempt)
    {
        var baseDelay = _options.ReconnectDelay;
        
        if (_options.UseExponentialBackoff)
        {
            var exponentialDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
            baseDelay = exponentialDelay > _options.MaxRetryDelay ? _options.MaxRetryDelay : exponentialDelay;
        }

        // Add jitter to prevent thundering herd
        if (_options.RetryJitterFactor > 0)
        {
            var jitterMs = baseDelay.TotalMilliseconds * _options.RetryJitterFactor * (_random.NextDouble() - 0.5) * 2;
            baseDelay = baseDelay.Add(TimeSpan.FromMilliseconds(jitterMs));
        }

        return baseDelay;
    }

    /// <summary>
    /// Determines if an exception indicates a connection problem
    /// </summary>
    private static bool IsConnectionException(Exception ex)
    {
        return ex is SocketException or IOException or ObjectDisposedException ||
               (ex is InvalidOperationException invalidOpEx && invalidOpEx.Message.Contains("not connected")) ||
               (ex.InnerException != null && IsConnectionException(ex.InnerException));
    }

    /// <summary>
    /// Handles connection loss and optionally initiates reconnection
    /// </summary>
    private async Task HandleConnectionLossAsync()
    {
        _logger.LogWarning("Connection lost to {Host}:{Port}", _options.Host, _options.Port);
        await DisconnectInternalAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Internal disconnect method without semaphore protection
    /// </summary>
    private async Task DisconnectInternalAsync()
    {
        _reconnectionCts?.Cancel();
        _reconnectionCts?.Dispose();
        _reconnectionCts = null;

        try
        {
            _modbusMaster?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing Modbus master");
        }
        finally
        {
            _modbusMaster = null;
        }

        try
        {
            if (_tcpClient?.Connected == true)
            {
                _tcpClient.Close();
            }
            _tcpClient?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing TCP client");
        }
        finally
        {
            _tcpClient = null;
        }

        await Task.CompletedTask;
        _logger.LogDebug("Disconnected from {Host}:{Port}", _options.Host, _options.Port);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;
        
        _logger.LogInformation("Disposing Modbus TCP client for {Host}:{Port}", _options.Host, _options.Port);

        try
        {
            await DisconnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect in dispose");
        }

        _connectionSemaphore.Dispose();
        _operationSemaphore.Dispose();
        _reconnectionCts?.Dispose();

        _logger.LogDebug("Modbus TCP client disposed");
    }
}