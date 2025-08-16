using FluentAssertions;
using ModbusClientLib.Options;
using ModbusClientLib.Codec;
using Xunit;

namespace ModbusClientLib.Tests;

/// <summary>
/// Tests for ModbusClientOptions configuration and validation
/// </summary>
public class ModbusClientOptionsTests
{
    [Fact]
    public void DefaultOptions_ShouldHaveCorrectValues()
    {
        // Arrange & Act
        var options = new ModbusClientOptions();

        // Assert
        options.Host.Should().Be("127.0.0.1");
        options.Port.Should().Be(502);
        options.SlaveId.Should().Be(1);
        options.ConnectTimeout.Should().Be(TimeSpan.FromSeconds(3));
        options.RequestTimeout.Should().Be(TimeSpan.FromSeconds(1));
        options.MaxRetries.Should().Be(3);
        options.ReconnectDelay.Should().Be(TimeSpan.FromSeconds(2));
        options.Endianness.Should().Be(ModbusEndianness.BigEndian);
        options.WordOrder.Should().Be(WordOrder.ABCD);
        options.AutoReconnect.Should().BeTrue();
        options.RetryJitterFactor.Should().Be(0.1);
        options.MaxRetryDelay.Should().Be(TimeSpan.FromSeconds(30));
        options.UseExponentialBackoff.Should().BeTrue();
        options.ClientId.Should().Be(Environment.MachineName);
    }

    [Fact]
    public void Validate_ValidOptions_ShouldNotThrow()
    {
        // Arrange
        var options = new ModbusClientOptions
        {
            Host = "192.168.1.100",
            Port = 502,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            RequestTimeout = TimeSpan.FromSeconds(2),
            MaxRetries = 5,
            ReconnectDelay = TimeSpan.FromSeconds(1),
            RetryJitterFactor = 0.2,
            MaxRetryDelay = TimeSpan.FromSeconds(60),
            ClientId = "TestClient"
        };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_InvalidHost_ShouldThrowArgumentException(string? host)
    {
        // Arrange
        var options = new ModbusClientOptions { Host = host! };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Host cannot be null or empty*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(100000)]
    public void Validate_InvalidPort_ShouldThrowArgumentException(int port)
    {
        // Arrange
        var options = new ModbusClientOptions { Port = port };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Port must be between 1 and 65535*");
    }

    [Fact]
    public void Validate_ZeroConnectTimeout_ShouldThrowArgumentException()
    {
        // Arrange
        var options = new ModbusClientOptions { ConnectTimeout = TimeSpan.Zero };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Connect timeout must be positive*");
    }

    [Fact]
    public void Validate_NegativeConnectTimeout_ShouldThrowArgumentException()
    {
        // Arrange
        var options = new ModbusClientOptions { ConnectTimeout = TimeSpan.FromSeconds(-1) };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Connect timeout must be positive*");
    }

    [Fact]
    public void Validate_ZeroRequestTimeout_ShouldThrowArgumentException()
    {
        // Arrange
        var options = new ModbusClientOptions { RequestTimeout = TimeSpan.Zero };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Request timeout must be positive*");
    }

    [Fact]
    public void Validate_NegativeMaxRetries_ShouldThrowArgumentException()
    {
        // Arrange
        var options = new ModbusClientOptions { MaxRetries = -1 };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Max retries cannot be negative*");
    }

    [Fact]
    public void Validate_NegativeReconnectDelay_ShouldThrowArgumentException()
    {
        // Arrange
        var options = new ModbusClientOptions { ReconnectDelay = TimeSpan.FromSeconds(-1) };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Reconnect delay cannot be negative*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void Validate_InvalidRetryJitterFactor_ShouldThrowArgumentException(double jitterFactor)
    {
        // Arrange
        var options = new ModbusClientOptions { RetryJitterFactor = jitterFactor };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Retry jitter factor must be between 0.0 and 1.0*");
    }

    [Fact]
    public void Validate_ZeroMaxRetryDelay_ShouldThrowArgumentException()
    {
        // Arrange
        var options = new ModbusClientOptions { MaxRetryDelay = TimeSpan.Zero };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Max retry delay must be positive*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_InvalidClientId_ShouldThrowArgumentException(string? clientId)
    {
        // Arrange
        var options = new ModbusClientOptions { ClientId = clientId! };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Client ID cannot be null or empty*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(65535)]
    public void Validate_ValidPortRange_ShouldNotThrow(int port)
    {
        // Arrange
        var options = new ModbusClientOptions { Port = port };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Validate_ValidRetryJitterFactor_ShouldNotThrow(double jitterFactor)
    {
        // Arrange
        var options = new ModbusClientOptions { RetryJitterFactor = jitterFactor };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().NotThrow();
    }

    [Fact]
    public void Validate_ZeroMaxRetries_ShouldNotThrow()
    {
        // Arrange
        var options = new ModbusClientOptions { MaxRetries = 0 };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().NotThrow();
    }

    [Fact]
    public void Validate_ZeroReconnectDelay_ShouldNotThrow()
    {
        // Arrange
        var options = new ModbusClientOptions { ReconnectDelay = TimeSpan.Zero };

        // Act & Assert
        var action = () => options.Validate();
        action.Should().NotThrow();
    }
}