namespace ModbusClientLib.Tcp;

/// <summary>
/// Exception thrown when Modbus operations fail
/// </summary>
public class ModbusException : Exception
{
    /// <summary>
    /// Modbus function code that caused the exception
    /// </summary>
    public byte? FunctionCode { get; }

    /// <summary>
    /// Modbus exception code if available
    /// </summary>
    public byte? ExceptionCode { get; }

    /// <summary>
    /// Device address that was being accessed
    /// </summary>
    public ushort? Address { get; }

    /// <summary>
    /// Initializes a new instance of the ModbusException class
    /// </summary>
    /// <param name="message">Error message</param>
    public ModbusException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the ModbusException class
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="innerException">Inner exception</param>
    public ModbusException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the ModbusException class with Modbus-specific information
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="functionCode">Modbus function code</param>
    /// <param name="exceptionCode">Modbus exception code</param>
    /// <param name="address">Device address</param>
    public ModbusException(string message, byte functionCode, byte exceptionCode, ushort address) : base(message)
    {
        FunctionCode = functionCode;
        ExceptionCode = exceptionCode;
        Address = address;
    }

    /// <summary>
    /// Initializes a new instance of the ModbusException class with Modbus-specific information
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="functionCode">Modbus function code</param>
    /// <param name="exceptionCode">Modbus exception code</param>
    /// <param name="address">Device address</param>
    /// <param name="innerException">Inner exception</param>
    public ModbusException(string message, byte functionCode, byte exceptionCode, ushort address, Exception innerException) 
        : base(message, innerException)
    {
        FunctionCode = functionCode;
        ExceptionCode = exceptionCode;
        Address = address;
    }
}

/// <summary>
/// Exception thrown when connection to Modbus server fails
/// </summary>
public class ModbusConnectionException : ModbusException
{
    /// <summary>
    /// Initializes a new instance of the ModbusConnectionException class
    /// </summary>
    /// <param name="message">Error message</param>
    public ModbusConnectionException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the ModbusConnectionException class
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="innerException">Inner exception</param>
    public ModbusConnectionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when Modbus operation times out
/// </summary>
public class ModbusTimeoutException : ModbusException
{
    /// <summary>
    /// Timeout duration that was exceeded
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Initializes a new instance of the ModbusTimeoutException class
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="timeout">Timeout duration</param>
    public ModbusTimeoutException(string message, TimeSpan timeout) : base(message)
    {
        Timeout = timeout;
    }

    /// <summary>
    /// Initializes a new instance of the ModbusTimeoutException class
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="timeout">Timeout duration</param>
    /// <param name="innerException">Inner exception</param>
    public ModbusTimeoutException(string message, TimeSpan timeout, Exception innerException) : base(message, innerException)
    {
        Timeout = timeout;
    }
}