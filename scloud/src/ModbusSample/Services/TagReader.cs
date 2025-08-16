using Microsoft.Extensions.Logging;
using ModbusClientLib.Abstractions;
using ModbusClientLib.Codec;
using ModbusSample.Models;

namespace ModbusSample.Services;

/// <summary>
/// Service for reading and writing typed tag values using the Modbus client
/// </summary>
public class TagReader
{
    private readonly IIndustrialModbusClient _modbusClient;
    private readonly ILogger<TagReader> _logger;

    public TagReader(IIndustrialModbusClient modbusClient, ILogger<TagReader> logger)
    {
        _modbusClient = modbusClient ?? throw new ArgumentNullException(nameof(modbusClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Reads a tag value and returns it as a typed object
    /// </summary>
    /// <param name="tagName">Name of the tag</param>
    /// <param name="tagConfig">Tag configuration</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tag value as an object</returns>
    public async Task<object?> ReadTagAsync(string tagName, TagConfig tagConfig, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tagConfig);
        tagConfig.Validate();

        _logger.LogDebug("Reading tag {TagName} at address {Address}", tagName, tagConfig.Address);

        try
        {
            return tagConfig.Type.ToLowerInvariant() switch
            {
                "coil" => await ReadCoilAsync(tagConfig, ct),
                "discrete" => await ReadDiscreteInputAsync(tagConfig, ct),
                "holding" => await ReadHoldingRegisterAsync(tagConfig, ct),
                "input" => await ReadInputRegisterAsync(tagConfig, ct),
                _ => throw new InvalidOperationException($"Unsupported tag type: {tagConfig.Type}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read tag {TagName}", tagName);
            throw;
        }
    }

    /// <summary>
    /// Writes a value to a tag
    /// </summary>
    /// <param name="tagName">Name of the tag</param>
    /// <param name="tagConfig">Tag configuration</param>
    /// <param name="value">Value to write</param>
    /// <param name="ct">Cancellation token</param>
    public async Task WriteTagAsync(string tagName, TagConfig tagConfig, object value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tagConfig);
        ArgumentNullException.ThrowIfNull(value);
        tagConfig.Validate();

        if (!tagConfig.Writable)
            throw new InvalidOperationException($"Tag {tagName} is not writable");

        _logger.LogDebug("Writing tag {TagName} at address {Address} with value {Value}", tagName, tagConfig.Address, value);

        try
        {
            switch (tagConfig.Type.ToLowerInvariant())
            {
                case "coil":
                    await WriteCoilAsync(tagConfig, Convert.ToBoolean(value), ct);
                    break;
                case "holding":
                    await WriteHoldingRegisterAsync(tagConfig, value, ct);
                    break;
                default:
                    throw new InvalidOperationException($"Cannot write to tag type: {tagConfig.Type}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write tag {TagName}", tagName);
            throw;
        }
    }

    /// <summary>
    /// Reads multiple tags in a single operation
    /// </summary>
    /// <param name="tags">Dictionary of tag names and configurations</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary of tag names and values</returns>
    public async Task<Dictionary<string, object?>> ReadTagsAsync(Dictionary<string, TagConfig> tags, CancellationToken ct = default)
    {
        var results = new Dictionary<string, object?>();

        foreach (var (tagName, tagConfig) in tags)
        {
            try
            {
                var value = await ReadTagAsync(tagName, tagConfig, ct);
                results[tagName] = value;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read tag {TagName}, skipping", tagName);
                results[tagName] = null;
            }
        }

        return results;
    }

    private async Task<bool> ReadCoilAsync(TagConfig tagConfig, CancellationToken ct)
    {
        var coils = await _modbusClient.ReadCoilsAsync(tagConfig.Address, 1, ct);
        return coils[0];
    }

    private async Task<bool> ReadDiscreteInputAsync(TagConfig tagConfig, CancellationToken ct)
    {
        var inputs = await _modbusClient.ReadDiscreteInputsAsync(tagConfig.Address, 1, ct);
        return inputs[0];
    }

    private async Task<object> ReadHoldingRegisterAsync(TagConfig tagConfig, CancellationToken ct)
    {
        var registers = await _modbusClient.ReadHoldingRegistersAsync(tagConfig.Address, (ushort)tagConfig.Length, ct);
        return ConvertRegistersToValue(registers, tagConfig);
    }

    private async Task<object> ReadInputRegisterAsync(TagConfig tagConfig, CancellationToken ct)
    {
        var registers = await _modbusClient.ReadInputRegistersAsync(tagConfig.Address, (ushort)tagConfig.Length, ct);
        return ConvertRegistersToValue(registers, tagConfig);
    }

    private async Task WriteCoilAsync(TagConfig tagConfig, bool value, CancellationToken ct)
    {
        await _modbusClient.WriteSingleCoilAsync(tagConfig.Address, value, ct);
    }

    private async Task WriteHoldingRegisterAsync(TagConfig tagConfig, object value, CancellationToken ct)
    {
        var registers = ConvertValueToRegisters(value, tagConfig);
        
        if (registers.Length == 1)
        {
            await _modbusClient.WriteSingleRegisterAsync(tagConfig.Address, registers[0], ct);
        }
        else
        {
            await _modbusClient.WriteMultipleRegistersAsync(tagConfig.Address, registers, ct);
        }
    }

    private object ConvertRegistersToValue(ushort[] registers, TagConfig tagConfig)
    {
        var endianness = tagConfig.GetEndianness();
        var wordOrder = tagConfig.GetWordOrder();

        var rawValue = tagConfig.DataType.ToLowerInvariant() switch
        {
            "int16" => (double)ModbusCodec.ToInt16(registers[0], endianness),
            "uint16" => (double)ModbusCodec.ToUInt16(registers[0], endianness),
            "int32" => (double)ModbusCodec.ToInt32(registers, endianness, wordOrder),
            "uint32" => (double)ModbusCodec.ToUInt32(registers, endianness, wordOrder),
            "float" => (double)ModbusCodec.ToFloat(registers, endianness, wordOrder),
            _ => throw new InvalidOperationException($"Unsupported data type: {tagConfig.DataType}")
        };

        // Apply scaling
        var engineeredValue = ModbusCodec.ApplyLinearScaling(rawValue, tagConfig.Scale, tagConfig.Offset);

        // Return the appropriate type
        return tagConfig.DataType.ToLowerInvariant() switch
        {
            "int16" => (short)Math.Round(engineeredValue),
            "uint16" => (ushort)Math.Round(engineeredValue),
            "int32" => (int)Math.Round(engineeredValue),
            "uint32" => (uint)Math.Round(engineeredValue),
            "float" => (float)engineeredValue,
            _ => engineeredValue
        };
    }

    private ushort[] ConvertValueToRegisters(object value, TagConfig tagConfig)
    {
        var endianness = tagConfig.GetEndianness();
        var wordOrder = tagConfig.GetWordOrder();

        // Convert to double and remove scaling
        var engineeredValue = Convert.ToDouble(value);
        var rawValue = ModbusCodec.RemoveLinearScaling(engineeredValue, tagConfig.Scale, tagConfig.Offset);

        return tagConfig.DataType.ToLowerInvariant() switch
        {
            "int16" => [ModbusCodec.FromInt16((short)Math.Round(rawValue), endianness)],
            "uint16" => [ModbusCodec.FromUInt16((ushort)Math.Round(rawValue), endianness)],
            "int32" => ModbusCodec.FromInt32((int)Math.Round(rawValue), endianness, wordOrder),
            "uint32" => ModbusCodec.FromUInt32((uint)Math.Round(rawValue), endianness, wordOrder),
            "float" => ModbusCodec.FromFloat((float)rawValue, endianness, wordOrder),
            _ => throw new InvalidOperationException($"Unsupported data type: {tagConfig.DataType}")
        };
    }

    /// <summary>
    /// Formats a tag value with its engineering unit
    /// </summary>
    /// <param name="tagConfig">Tag configuration</param>
    /// <param name="value">Tag value</param>
    /// <returns>Formatted string</returns>
    public static string FormatTagValue(TagConfig tagConfig, object? value)
    {
        if (value == null)
            return "NULL";

        var valueStr = value switch
        {
            bool b => b ? "TRUE" : "FALSE",
            float f => f.ToString("F3"),
            double d => d.ToString("F3"),
            _ => value.ToString() ?? "NULL"
        };

        return string.IsNullOrEmpty(tagConfig.Unit) 
            ? valueStr 
            : $"{valueStr} {tagConfig.Unit}";
    }
}