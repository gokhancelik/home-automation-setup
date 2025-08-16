using FluentAssertions;
using ModbusClientLib.Codec;
using Xunit;

namespace ModbusClientLib.Tests;

/// <summary>
/// Tests for ModbusCodec data type conversion functionality
/// </summary>
public class ModbusCodecTests
{
    #region Float Conversion Tests

    [Fact]
    public void ToFloat_BigEndian_ABCD_ShouldConvertCorrectly()
    {
        // Arrange - IEEE 754 representation of 123.45f
        var registers = new ushort[] { 0x42F6, 0xE666 };

        // Act
        var result = ModbusCodec.ToFloat(registers, ModbusEndianness.BigEndian, WordOrder.ABCD);

        // Assert
        result.Should().BeApproximately(123.45f, 0.001f);
    }

    [Fact]
    public void ToFloat_LittleEndian_BADC_ShouldConvertCorrectly()
    {
        // Arrange
        var registers = new ushort[] { 0xE666, 0x42F6 };

        // Act
        var result = ModbusCodec.ToFloat(registers, ModbusEndianness.LittleEndian, WordOrder.BADC);

        // Assert
        result.Should().BeApproximately(123.45f, 0.001f);
    }

    [Fact]
    public void FromFloat_ToFloat_RoundTrip_ShouldPreserveValue()
    {
        // Arrange
        var originalValue = 456.789f;

        // Act
        var registers = ModbusCodec.FromFloat(originalValue, ModbusEndianness.BigEndian, WordOrder.ABCD);
        var convertedBack = ModbusCodec.ToFloat(registers, ModbusEndianness.BigEndian, WordOrder.ABCD);

        // Assert
        convertedBack.Should().BeApproximately(originalValue, 0.001f);
    }

    [Theory]
    [InlineData(ModbusEndianness.BigEndian, WordOrder.ABCD)]
    [InlineData(ModbusEndianness.BigEndian, WordOrder.BADC)]
    [InlineData(ModbusEndianness.BigEndian, WordOrder.CDAB)]
    [InlineData(ModbusEndianness.BigEndian, WordOrder.DCBA)]
    [InlineData(ModbusEndianness.LittleEndian, WordOrder.ABCD)]
    [InlineData(ModbusEndianness.LittleEndian, WordOrder.BADC)]
    [InlineData(ModbusEndianness.LittleEndian, WordOrder.CDAB)]
    [InlineData(ModbusEndianness.LittleEndian, WordOrder.DCBA)]
    public void Float_AllEndiannessCombinations_ShouldRoundTrip(ModbusEndianness endianness, WordOrder wordOrder)
    {
        // Arrange
        var testValues = new[] { 0.0f, 1.0f, -1.0f, 123.456f, -789.012f, float.MaxValue, float.MinValue };

        foreach (var originalValue in testValues)
        {
            // Act
            var registers = ModbusCodec.FromFloat(originalValue, endianness, wordOrder);
            var convertedBack = ModbusCodec.ToFloat(registers, endianness, wordOrder);

            // Assert
            convertedBack.Should().BeApproximately(originalValue, 0.001f, 
                $"Failed for value {originalValue} with {endianness}/{wordOrder}");
        }
    }

    [Fact]
    public void ToFloat_InvalidRegisterCount_ShouldThrowException()
    {
        // Arrange
        var registers = new ushort[] { 0x1234 }; // Only 1 register

        // Act & Assert
        var action = () => ModbusCodec.ToFloat(registers);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*exactly 2 registers*");
    }

    #endregion

    #region Int32 Conversion Tests

    [Fact]
    public void ToInt32_BigEndian_ABCD_ShouldConvertCorrectly()
    {
        // Arrange - 305419896 in big-endian format
        var registers = new ushort[] { 0x1234, 0x5678 };

        // Act
        var result = ModbusCodec.ToInt32(registers, ModbusEndianness.BigEndian, WordOrder.ABCD);

        // Assert
        result.Should().Be(0x12345678);
    }

    [Fact]
    public void ToInt32_NegativeValue_ShouldConvertCorrectly()
    {
        // Arrange
        var originalValue = -123456789;
        var registers = ModbusCodec.FromInt32(originalValue);

        // Act
        var result = ModbusCodec.ToInt32(registers);

        // Assert
        result.Should().Be(originalValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    [InlineData(123456789)]
    [InlineData(-987654321)]
    public void Int32_RoundTrip_ShouldPreserveValue(int originalValue)
    {
        // Act
        var registers = ModbusCodec.FromInt32(originalValue);
        var convertedBack = ModbusCodec.ToInt32(registers);

        // Assert
        convertedBack.Should().Be(originalValue);
    }

    #endregion

    #region UInt32 Conversion Tests

    [Fact]
    public void ToUInt32_BigEndian_ABCD_ShouldConvertCorrectly()
    {
        // Arrange
        var registers = new ushort[] { 0x1234, 0x5678 };

        // Act
        var result = ModbusCodec.ToUInt32(registers, ModbusEndianness.BigEndian, WordOrder.ABCD);

        // Assert
        result.Should().Be(0x12345678u);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(uint.MaxValue)]
    [InlineData(123456789u)]
    [InlineData(4000000000u)]
    public void UInt32_RoundTrip_ShouldPreserveValue(uint originalValue)
    {
        // Act
        var registers = ModbusCodec.FromUInt32(originalValue);
        var convertedBack = ModbusCodec.ToUInt32(registers);

        // Assert
        convertedBack.Should().Be(originalValue);
    }

    #endregion

    #region Int16/UInt16 Conversion Tests

    [Fact]
    public void Int16_BigEndian_ShouldConvertCorrectly()
    {
        // Arrange
        short originalValue = -12345;

        // Act
        var register = ModbusCodec.FromInt16(originalValue, ModbusEndianness.BigEndian);
        var convertedBack = ModbusCodec.ToInt16(register, ModbusEndianness.BigEndian);

        // Assert
        convertedBack.Should().Be(originalValue);
    }

    [Fact]
    public void UInt16_LittleEndian_ShouldConvertCorrectly()
    {
        // Arrange
        ushort originalValue = 54321;

        // Act
        var register = ModbusCodec.FromUInt16(originalValue, ModbusEndianness.LittleEndian);
        var convertedBack = ModbusCodec.ToUInt16(register, ModbusEndianness.LittleEndian);

        // Assert
        convertedBack.Should().Be(originalValue);
    }

    #endregion

    #region Engineering Units Tests

    [Fact]
    public void ApplyLinearScaling_ShouldCalculateCorrectly()
    {
        // Arrange
        var rawValue = 100.0;
        var scale = 0.1;
        var offset = 5.0;

        // Act
        var result = ModbusCodec.ApplyLinearScaling(rawValue, scale, offset);

        // Assert
        result.Should().BeApproximately(15.0, 0.001); // (100 * 0.1) + 5 = 15
    }

    [Fact]
    public void RemoveLinearScaling_ShouldCalculateCorrectly()
    {
        // Arrange
        var engineeredValue = 15.0;
        var scale = 0.1;
        var offset = 5.0;

        // Act
        var result = ModbusCodec.RemoveLinearScaling(engineeredValue, scale, offset);

        // Assert
        result.Should().BeApproximately(100.0, 0.001); // (15 - 5) / 0.1 = 100
    }

    [Fact]
    public void LinearScaling_RoundTrip_ShouldPreserveValue()
    {
        // Arrange
        var originalValue = 250.5;
        var scale = 0.25;
        var offset = -10.0;

        // Act
        var rawValue = ModbusCodec.RemoveLinearScaling(originalValue, scale, offset);
        var convertedBack = ModbusCodec.ApplyLinearScaling(rawValue, scale, offset);

        // Assert
        convertedBack.Should().BeApproximately(originalValue, 0.001);
    }

    [Fact]
    public void ScaleRegister_UnsignedValue_ShouldCalculateCorrectly()
    {
        // Arrange
        ushort register = 1000;
        var scale = 0.01;
        var offset = 0.0;

        // Act
        var result = ModbusCodec.ScaleRegister(register, scale, offset, isSigned: false);

        // Assert
        result.Should().BeApproximately(10.0, 0.001); // 1000 * 0.01 = 10
    }

    [Fact]
    public void ScaleRegister_SignedValue_ShouldCalculateCorrectly()
    {
        // Arrange
        ushort register = ModbusCodec.FromInt16(-1000);
        var scale = 0.01;
        var offset = 0.0;

        // Act
        var result = ModbusCodec.ScaleRegister(register, scale, offset, isSigned: true);

        // Assert
        result.Should().BeApproximately(-10.0, 0.001); // -1000 * 0.01 = -10
    }

    [Fact]
    public void UnscaleToRegister_ShouldConvertBackCorrectly()
    {
        // Arrange
        var engineeredValue = 25.5;
        var scale = 0.1;
        var offset = 0.0;

        // Act
        var register = ModbusCodec.UnscaleToRegister(engineeredValue, scale, offset, isSigned: false);
        var convertedBack = ModbusCodec.ScaleRegister(register, scale, offset, isSigned: false);

        // Assert
        convertedBack.Should().BeApproximately(engineeredValue, 0.1); // Allow for rounding
    }

    [Theory]
    [InlineData(100.0, 0.1, 5.0, false)]
    [InlineData(-50.0, 0.5, -10.0, true)]
    [InlineData(0.0, 1.0, 0.0, false)]
    [InlineData(1000.0, 0.001, 100.0, false)]
    public void RegisterScaling_RoundTrip_ShouldPreserveValue(double engineeredValue, double scale, double offset, bool isSigned)
    {
        // Act
        var register = ModbusCodec.UnscaleToRegister(engineeredValue, scale, offset, isSigned);
        var convertedBack = ModbusCodec.ScaleRegister(register, scale, offset, isSigned);

        // Assert
        convertedBack.Should().BeApproximately(engineeredValue, Math.Max(0.1, Math.Abs(engineeredValue * 0.001)));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ToFloat_SpecialValues_ShouldHandleCorrectly()
    {
        // Test NaN
        var nanRegisters = ModbusCodec.FromFloat(float.NaN);
        var nanResult = ModbusCodec.ToFloat(nanRegisters);
        nanResult.Should().Be(float.NaN);

        // Test Positive Infinity
        var posInfRegisters = ModbusCodec.FromFloat(float.PositiveInfinity);
        var posInfResult = ModbusCodec.ToFloat(posInfRegisters);
        posInfResult.Should().Be(float.PositiveInfinity);

        // Test Negative Infinity
        var negInfRegisters = ModbusCodec.FromFloat(float.NegativeInfinity);
        var negInfResult = ModbusCodec.ToFloat(negInfRegisters);
        negInfResult.Should().Be(float.NegativeInfinity);
    }

    [Fact]
    public void UnscaleToRegister_ValueOutOfRange_ShouldClamp()
    {
        // Arrange - value that would exceed uint16 range
        var engineeredValue = 100000.0;
        var scale = 1.0;
        var offset = 0.0;

        // Act
        var register = ModbusCodec.UnscaleToRegister(engineeredValue, scale, offset, isSigned: false);

        // Assert
        register.Should().Be(ushort.MaxValue); // Should be clamped
    }

    [Fact]
    public void UnscaleToRegister_SignedValueOutOfRange_ShouldClamp()
    {
        // Arrange - value that would exceed int16 range
        var engineeredValue = -100000.0;
        var scale = 1.0;
        var offset = 0.0;

        // Act
        var register = ModbusCodec.UnscaleToRegister(engineeredValue, scale, offset, isSigned: true);
        var convertedBack = ModbusCodec.ToInt16(register);

        // Assert
        convertedBack.Should().Be(short.MinValue); // Should be clamped
    }

    #endregion
}