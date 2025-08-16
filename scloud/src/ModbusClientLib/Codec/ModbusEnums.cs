namespace ModbusClientLib.Codec;

/// <summary>
/// Specifies the byte order for multi-byte data types
/// </summary>
public enum ModbusEndianness
{
    /// <summary>
    /// Big-endian byte order (most significant byte first)
    /// </summary>
    BigEndian,

    /// <summary>
    /// Little-endian byte order (least significant byte first)
    /// </summary>
    LittleEndian
}

/// <summary>
/// Specifies the word order for 32-bit data types
/// </summary>
public enum WordOrder
{
    /// <summary>
    /// ABCD word order (first register contains most significant word)
    /// </summary>
    ABCD,

    /// <summary>
    /// BADC word order (first register contains least significant word)
    /// </summary>
    BADC,

    /// <summary>
    /// CDAB word order (swapped word order)
    /// </summary>
    CDAB,

    /// <summary>
    /// DCBA word order (completely reversed)
    /// </summary>
    DCBA
}