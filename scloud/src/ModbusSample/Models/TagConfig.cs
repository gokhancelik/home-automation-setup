using System.Text.Json.Serialization;
using ModbusClientLib.Codec;

namespace ModbusSample.Models;

/// <summary>
/// Represents a tag configuration for reading/writing Modbus data
/// </summary>
public class TagConfig
{
    /// <summary>
    /// Type of Modbus data (coil, discrete, holding, input)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Modbus address
    /// </summary>
    [JsonPropertyName("address")]
    public ushort Address { get; set; }

    /// <summary>
    /// Number of registers (for multi-register data types)
    /// </summary>
    [JsonPropertyName("length")]
    public int Length { get; set; } = 1;

    /// <summary>
    /// Data type (int16, uint16, int32, uint32, float)
    /// </summary>
    [JsonPropertyName("datatype")]
    public string DataType { get; set; } = "uint16";

    /// <summary>
    /// Scale factor for engineering units
    /// </summary>
    [JsonPropertyName("scale")]
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// Offset for engineering units
    /// </summary>
    [JsonPropertyName("offset")]
    public double Offset { get; set; } = 0.0;

    /// <summary>
    /// Engineering unit description
    /// </summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    /// <summary>
    /// Tag description
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Endianness for this specific tag (overrides global setting)
    /// </summary>
    [JsonPropertyName("endianness")]
    public string? Endianness { get; set; }

    /// <summary>
    /// Word order for this specific tag (overrides global setting)
    /// </summary>
    [JsonPropertyName("wordOrder")]
    public string? WordOrder { get; set; }

    /// <summary>
    /// Whether the tag is writable
    /// </summary>
    [JsonPropertyName("writable")]
    public bool Writable { get; set; } = true;

    /// <summary>
    /// Gets the ModbusEndianness enum value
    /// </summary>
    public ModbusEndianness GetEndianness(ModbusEndianness defaultEndianness = ModbusEndianness.BigEndian)
    {
        if (string.IsNullOrEmpty(Endianness))
            return defaultEndianness;

        return Enum.TryParse<ModbusEndianness>(Endianness, true, out var result) 
            ? result 
            : defaultEndianness;
    }

    /// <summary>
    /// Gets the WordOrder enum value
    /// </summary>
    public WordOrder GetWordOrder(WordOrder defaultWordOrder = ModbusClientLib.Codec.WordOrder.ABCD)
    {
        if (string.IsNullOrEmpty(this.WordOrder))
            return defaultWordOrder;

        return Enum.TryParse<WordOrder>(this.WordOrder, true, out var result) 
            ? result 
            : defaultWordOrder;
    }

    /// <summary>
    /// Gets whether the data type represents a signed value
    /// </summary>
    public bool IsSigned => DataType.ToLowerInvariant() switch
    {
        "int16" => true,
        "int32" => true,
        "float" => false, // Float conversion handles sign separately
        _ => false
    };

    /// <summary>
    /// Validates the tag configuration
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Type))
            throw new InvalidOperationException("Tag type cannot be empty");

        var validTypes = new[] { "coil", "discrete", "holding", "input" };
        if (!validTypes.Contains(Type.ToLowerInvariant()))
            throw new InvalidOperationException($"Invalid tag type: {Type}. Valid types: {string.Join(", ", validTypes)}");

        if (Length < 1 || Length > 4)
            throw new InvalidOperationException("Length must be between 1 and 4");

        var validDataTypes = new[] { "int16", "uint16", "int32", "uint32", "float" };
        if (!validDataTypes.Contains(DataType.ToLowerInvariant()))
            throw new InvalidOperationException($"Invalid data type: {DataType}. Valid types: {string.Join(", ", validDataTypes)}");

        // Validate length requirements for data types
        var requiredLength = DataType.ToLowerInvariant() switch
        {
            "int16" or "uint16" => 1,
            "int32" or "uint32" or "float" => 2,
            _ => 1
        };

        if (Length != requiredLength)
            throw new InvalidOperationException($"Data type {DataType} requires length of {requiredLength}");

        // Validate write access for read-only register types
        if (Writable && Type.ToLowerInvariant() is "discrete" or "input")
            throw new InvalidOperationException($"Cannot write to {Type} registers - they are read-only");
    }
}