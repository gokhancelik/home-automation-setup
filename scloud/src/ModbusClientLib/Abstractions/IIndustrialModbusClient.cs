namespace ModbusClientLib.Abstractions;

/// <summary>
/// Represents an industrial Modbus TCP client for communicating with PLCs and other devices
/// </summary>
public interface IIndustrialModbusClient : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the client is currently connected
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects to the Modbus TCP server
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the connection operation</returns>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Disconnects from the Modbus TCP server
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the disconnection operation</returns>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Reads coils (discrete outputs) from the device
    /// </summary>
    /// <param name="startAddress">Starting address</param>
    /// <param name="count">Number of coils to read</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Array of boolean values representing coil states</returns>
    Task<bool[]> ReadCoilsAsync(ushort startAddress, ushort count, CancellationToken ct = default);

    /// <summary>
    /// Reads discrete inputs from the device
    /// </summary>
    /// <param name="startAddress">Starting address</param>
    /// <param name="count">Number of discrete inputs to read</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Array of boolean values representing discrete input states</returns>
    Task<bool[]> ReadDiscreteInputsAsync(ushort startAddress, ushort count, CancellationToken ct = default);

    /// <summary>
    /// Reads holding registers from the device
    /// </summary>
    /// <param name="startAddress">Starting address</param>
    /// <param name="count">Number of registers to read</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Array of 16-bit unsigned integers representing register values</returns>
    Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort count, CancellationToken ct = default);

    /// <summary>
    /// Reads input registers from the device
    /// </summary>
    /// <param name="startAddress">Starting address</param>
    /// <param name="count">Number of registers to read</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Array of 16-bit unsigned integers representing register values</returns>
    Task<ushort[]> ReadInputRegistersAsync(ushort startAddress, ushort count, CancellationToken ct = default);

    /// <summary>
    /// Writes a single coil to the device
    /// </summary>
    /// <param name="address">Coil address</param>
    /// <param name="value">Boolean value to write</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the write operation</returns>
    Task WriteSingleCoilAsync(ushort address, bool value, CancellationToken ct = default);

    /// <summary>
    /// Writes a single register to the device
    /// </summary>
    /// <param name="address">Register address</param>
    /// <param name="value">16-bit unsigned integer value to write</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the write operation</returns>
    Task WriteSingleRegisterAsync(ushort address, ushort value, CancellationToken ct = default);

    /// <summary>
    /// Writes multiple registers to the device
    /// </summary>
    /// <param name="startAddress">Starting address</param>
    /// <param name="values">Array of 16-bit unsigned integer values to write</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the write operation</returns>
    Task WriteMultipleRegistersAsync(ushort startAddress, ushort[] values, CancellationToken ct = default);

    /// <summary>
    /// Writes multiple coils to the device
    /// </summary>
    /// <param name="startAddress">Starting address</param>
    /// <param name="values">Array of boolean values to write</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task representing the write operation</returns>
    Task WriteMultipleCoilsAsync(ushort startAddress, bool[] values, CancellationToken ct = default);
}