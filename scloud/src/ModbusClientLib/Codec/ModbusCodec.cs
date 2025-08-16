using System.Runtime.InteropServices;

namespace ModbusClientLib.Codec;

/// <summary>
/// Provides static methods for converting between Modbus register data and various data types
/// with support for different endianness and word order configurations
/// </summary>
public static class ModbusCodec
{
    #region Float Conversion

    /// <summary>
    /// Converts two 16-bit registers to a 32-bit floating point value
    /// </summary>
    /// <param name="registers">Array containing two registers</param>
    /// <param name="endianness">Byte order within each register</param>
    /// <param name="wordOrder">Word order for 32-bit values</param>
    /// <returns>32-bit floating point value</returns>
    /// <exception cref="ArgumentException">Thrown when registers array doesn't contain exactly 2 elements</exception>
    public static float ToFloat(ushort[] registers, ModbusEndianness endianness = ModbusEndianness.BigEndian, WordOrder wordOrder = WordOrder.ABCD)
    {
        if (registers.Length != 2)
            throw new ArgumentException("Float conversion requires exactly 2 registers", nameof(registers));

        var bytes = RegistersToBytes(registers, endianness, wordOrder);
        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>
    /// Converts a 32-bit floating point value to two 16-bit registers
    /// </summary>
    /// <param name="value">32-bit floating point value</param>
    /// <param name="endianness">Byte order within each register</param>
    /// <param name="wordOrder">Word order for 32-bit values</param>
    /// <returns>Array of two 16-bit registers</returns>
    public static ushort[] FromFloat(float value, ModbusEndianness endianness = ModbusEndianness.BigEndian, WordOrder wordOrder = WordOrder.ABCD)
    {
        var bytes = BitConverter.GetBytes(value);
        return BytesToRegisters(bytes, endianness, wordOrder);
    }

    #endregion

    #region Int32 Conversion

    /// <summary>
    /// Converts two 16-bit registers to a 32-bit signed integer
    /// </summary>
    /// <param name="registers">Array containing two registers</param>
    /// <param name="endianness">Byte order within each register</param>
    /// <param name="wordOrder">Word order for 32-bit values</param>
    /// <returns>32-bit signed integer</returns>
    /// <exception cref="ArgumentException">Thrown when registers array doesn't contain exactly 2 elements</exception>
    public static int ToInt32(ushort[] registers, ModbusEndianness endianness = ModbusEndianness.BigEndian, WordOrder wordOrder = WordOrder.ABCD)
    {
        if (registers.Length != 2)
            throw new ArgumentException("Int32 conversion requires exactly 2 registers", nameof(registers));

        var bytes = RegistersToBytes(registers, endianness, wordOrder);
        return BitConverter.ToInt32(bytes, 0);
    }

    /// <summary>
    /// Converts a 32-bit signed integer to two 16-bit registers
    /// </summary>
    /// <param name="value">32-bit signed integer</param>
    /// <param name="endianness">Byte order within each register</param>
    /// <param name="wordOrder">Word order for 32-bit values</param>
    /// <returns>Array of two 16-bit registers</returns>
    public static ushort[] FromInt32(int value, ModbusEndianness endianness = ModbusEndianness.BigEndian, WordOrder wordOrder = WordOrder.ABCD)
    {
        var bytes = BitConverter.GetBytes(value);
        return BytesToRegisters(bytes, endianness, wordOrder);
    }

    #endregion

    #region UInt32 Conversion

    /// <summary>
    /// Converts two 16-bit registers to a 32-bit unsigned integer
    /// </summary>
    /// <param name="registers">Array containing two registers</param>
    /// <param name="endianness">Byte order within each register</param>
    /// <param name="wordOrder">Word order for 32-bit values</param>
    /// <returns>32-bit unsigned integer</returns>
    /// <exception cref="ArgumentException">Thrown when registers array doesn't contain exactly 2 elements</exception>
    public static uint ToUInt32(ushort[] registers, ModbusEndianness endianness = ModbusEndianness.BigEndian, WordOrder wordOrder = WordOrder.ABCD)
    {
        if (registers.Length != 2)
            throw new ArgumentException("UInt32 conversion requires exactly 2 registers", nameof(registers));

        var bytes = RegistersToBytes(registers, endianness, wordOrder);
        return BitConverter.ToUInt32(bytes, 0);
    }

    /// <summary>
    /// Converts a 32-bit unsigned integer to two 16-bit registers
    /// </summary>
    /// <param name="value">32-bit unsigned integer</param>
    /// <param name="endianness">Byte order within each register</param>
    /// <param name="wordOrder">Word order for 32-bit values</param>
    /// <returns>Array of two 16-bit registers</returns>
    public static ushort[] FromUInt32(uint value, ModbusEndianness endianness = ModbusEndianness.BigEndian, WordOrder wordOrder = WordOrder.ABCD)
    {
        var bytes = BitConverter.GetBytes(value);
        return BytesToRegisters(bytes, endianness, wordOrder);
    }

    #endregion

    #region Int16/UInt16 Conversion

    /// <summary>
    /// Converts a 16-bit register to a signed integer with endianness handling
    /// </summary>
    /// <param name="register">16-bit register value</param>
    /// <param name="endianness">Byte order within the register</param>
    /// <returns>16-bit signed integer</returns>
    public static short ToInt16(ushort register, ModbusEndianness endianness = ModbusEndianness.BigEndian)
    {
        if (endianness == ModbusEndianness.LittleEndian)
        {
            var bytes = BitConverter.GetBytes(register);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }
        return (short)register;
    }

    /// <summary>
    /// Converts a signed integer to a 16-bit register with endianness handling
    /// </summary>
    /// <param name="value">16-bit signed integer</param>
    /// <param name="endianness">Byte order within the register</param>
    /// <returns>16-bit register value</returns>
    public static ushort FromInt16(short value, ModbusEndianness endianness = ModbusEndianness.BigEndian)
    {
        if (endianness == ModbusEndianness.LittleEndian)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }
        return (ushort)value;
    }

    /// <summary>
    /// Converts a 16-bit register to an unsigned integer with endianness handling
    /// </summary>
    /// <param name="register">16-bit register value</param>
    /// <param name="endianness">Byte order within the register</param>
    /// <returns>16-bit unsigned integer</returns>
    public static ushort ToUInt16(ushort register, ModbusEndianness endianness = ModbusEndianness.BigEndian)
    {
        if (endianness == ModbusEndianness.LittleEndian)
        {
            var bytes = BitConverter.GetBytes(register);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }
        return register;
    }

    /// <summary>
    /// Converts an unsigned integer to a 16-bit register with endianness handling
    /// </summary>
    /// <param name="value">16-bit unsigned integer</param>
    /// <param name="endianness">Byte order within the register</param>
    /// <returns>16-bit register value</returns>
    public static ushort FromUInt16(ushort value, ModbusEndianness endianness = ModbusEndianness.BigEndian)
    {
        if (endianness == ModbusEndianness.LittleEndian)
        {
            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return BitConverter.ToUInt16(bytes, 0);
        }
        return value;
    }

    #endregion

    #region Engineering Units

    /// <summary>
    /// Applies linear scaling to a raw value: (raw * scale) + offset
    /// </summary>
    /// <param name="rawValue">Raw value from the device</param>
    /// <param name="scale">Scale factor</param>
    /// <param name="offset">Offset value</param>
    /// <returns>Scaled engineering value</returns>
    public static double ApplyLinearScaling(double rawValue, double scale, double offset)
    {
        return (rawValue * scale) + offset;
    }

    /// <summary>
    /// Removes linear scaling from an engineering value: (engineered - offset) / scale
    /// </summary>
    /// <param name="engineeredValue">Engineering value</param>
    /// <param name="scale">Scale factor</param>
    /// <param name="offset">Offset value</param>
    /// <returns>Raw value for the device</returns>
    public static double RemoveLinearScaling(double engineeredValue, double scale, double offset)
    {
        return (engineeredValue - offset) / scale;
    }

    /// <summary>
    /// Scales a 16-bit register value to engineering units
    /// </summary>
    /// <param name="register">Raw register value</param>
    /// <param name="scale">Scale factor</param>
    /// <param name="offset">Offset value</param>
    /// <param name="isSigned">Whether to treat the register as signed</param>
    /// <param name="endianness">Byte order within the register</param>
    /// <returns>Scaled engineering value</returns>
    public static double ScaleRegister(ushort register, double scale, double offset, bool isSigned = false, ModbusEndianness endianness = ModbusEndianness.BigEndian)
    {
        double rawValue = isSigned ? ToInt16(register, endianness) : ToUInt16(register, endianness);
        return ApplyLinearScaling(rawValue, scale, offset);
    }

    /// <summary>
    /// Converts an engineering value back to a 16-bit register
    /// </summary>
    /// <param name="engineeredValue">Engineering value</param>
    /// <param name="scale">Scale factor</param>
    /// <param name="offset">Offset value</param>
    /// <param name="isSigned">Whether to treat the register as signed</param>
    /// <param name="endianness">Byte order within the register</param>
    /// <returns>Raw register value</returns>
    public static ushort UnscaleToRegister(double engineeredValue, double scale, double offset, bool isSigned = false, ModbusEndianness endianness = ModbusEndianness.BigEndian)
    {
        var rawValue = RemoveLinearScaling(engineeredValue, scale, offset);
        
        if (isSigned)
        {
            var clampedValue = Math.Max(short.MinValue, Math.Min(short.MaxValue, Math.Round(rawValue)));
            return FromInt16((short)clampedValue, endianness);
        }
        else
        {
            var clampedValue = Math.Max(ushort.MinValue, Math.Min(ushort.MaxValue, Math.Round(rawValue)));
            return FromUInt16((ushort)clampedValue, endianness);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Converts registers to bytes with specified endianness and word order
    /// </summary>
    private static byte[] RegistersToBytes(ushort[] registers, ModbusEndianness endianness, WordOrder wordOrder)
    {
        var bytes = new byte[registers.Length * 2];
        
        // Convert registers to bytes
        for (int i = 0; i < registers.Length; i++)
        {
            var regBytes = BitConverter.GetBytes(registers[i]);
            
            if (endianness == ModbusEndianness.BigEndian)
            {
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(regBytes);
            }
            else
            {
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(regBytes);
            }
            
            bytes[i * 2] = regBytes[0];
            bytes[i * 2 + 1] = regBytes[1];
        }
        
        // Apply word order for 32-bit values
        if (registers.Length == 2)
        {
            bytes = ApplyWordOrder(bytes, wordOrder);
        }
        
        return bytes;
    }

    /// <summary>
    /// Converts bytes to registers with specified endianness and word order
    /// </summary>
    private static ushort[] BytesToRegisters(byte[] bytes, ModbusEndianness endianness, WordOrder wordOrder)
    {
        if (bytes.Length % 2 != 0)
            throw new ArgumentException("Byte array length must be even", nameof(bytes));

        // Apply word order for 32-bit values
        if (bytes.Length == 4)
        {
            bytes = ApplyWordOrder(bytes, wordOrder);
        }

        var registers = new ushort[bytes.Length / 2];
        
        for (int i = 0; i < registers.Length; i++)
        {
            var regBytes = new byte[] { bytes[i * 2], bytes[i * 2 + 1] };
            
            if (endianness == ModbusEndianness.BigEndian)
            {
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(regBytes);
            }
            else
            {
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(regBytes);
            }
            
            registers[i] = BitConverter.ToUInt16(regBytes, 0);
        }
        
        return registers;
    }

    /// <summary>
    /// Applies word order transformation for 32-bit values
    /// </summary>
    private static byte[] ApplyWordOrder(byte[] bytes, WordOrder wordOrder)
    {
        if (bytes.Length != 4)
            return bytes;

        return wordOrder switch
        {
            WordOrder.ABCD => bytes, // No change
            WordOrder.BADC => [bytes[2], bytes[3], bytes[0], bytes[1]],
            WordOrder.CDAB => [bytes[1], bytes[0], bytes[3], bytes[2]],
            WordOrder.DCBA => [bytes[3], bytes[2], bytes[1], bytes[0]],
            _ => bytes
        };
    }

    #endregion
}