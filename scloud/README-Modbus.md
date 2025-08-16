# Modbus TCP Client Library

A production-ready Modbus TCP client library for .NET 8 with comprehensive features for industrial automation applications, including support for Factory I/O and other industrial simulators.

## Features

- **Full Modbus TCP Support**: Read/write coils, discrete inputs, holding registers, and input registers
- **Data Type Conversion**: Support for int16/uint16/int32/uint32/float32 with configurable endianness and word order
- **Engineering Units**: Linear scaling with offset and scale factors
- **Resilient Communication**: Automatic retry with exponential backoff, timeout handling, and auto-reconnection
- **Async/Await**: Fully asynchronous with proper cancellation token support
- **Dependency Injection**: Easy integration with Microsoft.Extensions.DependencyInjection
- **Comprehensive Logging**: Detailed logging using Microsoft.Extensions.Logging
- **Clean Architecture**: Well-structured with abstractions, options pattern, and separation of concerns
- **Cross-Platform**: Runs on Windows and Linux (.NET 8)

## Quick Start

### Basic Usage

```csharp
using ModbusClientLib.Extensions;
using ModbusClientLib.Abstractions;

// Configure services
var services = new ServiceCollection()
    .AddLogging(builder => builder.AddConsole())
    .AddModbusClient(options =>
    {
        options.Host = "127.0.0.1";
        options.Port = 502;
        options.SlaveId = 1;
    })
    .BuildServiceProvider();

// Get client and connect
var client = services.GetRequiredService<IIndustrialModbusClient>();
await client.ConnectAsync();

// Read/write operations
var coils = await client.ReadCoilsAsync(0, 10);
await client.WriteSingleCoilAsync(0, true);

var registers = await client.ReadHoldingRegistersAsync(40001, 2);
await client.WriteSingleRegisterAsync(40001, 1234);
```

### Data Type Conversion

```csharp
using ModbusClientLib.Codec;

// Read float value from two registers
var registers = await client.ReadHoldingRegistersAsync(40010, 2);
var floatValue = ModbusCodec.ToFloat(registers, ModbusEndianness.BigEndian, WordOrder.ABCD);

// Apply engineering scaling
var scaledValue = ModbusCodec.ApplyLinearScaling(floatValue, scale: 0.1, offset: -40.0);

// Write scaled value back
var rawValue = ModbusCodec.RemoveLinearScaling(scaledValue, scale: 0.1, offset: -40.0);
var writeRegisters = ModbusCodec.FromFloat((float)rawValue);
await client.WriteMultipleRegistersAsync(40010, writeRegisters);
```

## Configuration

### appsettings.json

```json
{
  "Modbus": {
    "Host": "127.0.0.1",
    "Port": 502,
    "SlaveId": 1,
    "ConnectTimeout": "00:00:05",
    "RequestTimeout": "00:00:02",
    "MaxRetries": 3,
    "ReconnectDelay": "00:00:02",
    "Endianness": "BigEndian",
    "WordOrder": "ABCD",
    "AutoReconnect": true,
    "RetryJitterFactor": 0.1,
    "MaxRetryDelay": "00:00:30",
    "UseExponentialBackoff": true,
    "ClientId": "MyClient"
  }
}
```

### Dependency Injection

```csharp
// From configuration
services.AddModbusClient(configuration.GetSection("Modbus"));

// Programmatic configuration
services.AddModbusClient(options =>
{
    options.Host = "192.168.1.100";
    options.Port = 502;
    options.ConnectTimeout = TimeSpan.FromSeconds(10);
});

// Named clients
services.AddModbusClient("PLC1", options => { options.Host = "192.168.1.10"; });
services.AddModbusClient("PLC2", options => { options.Host = "192.168.1.20"; });
```

## Factory I/O Integration

The library includes a comprehensive sample demonstrating integration with Factory I/O, a popular industrial simulation platform.

### Starting Factory I/O as Modbus TCP Server

1. **Install Factory I/O**: Download from [https://factoryio.com/](https://factoryio.com/)
2. **Configure Modbus TCP**:
   - Open Factory I/O
   - Go to `FILE > Drivers`
   - Select `Modbus TCP/IP Server`
   - Configure server settings:
     - **IP Address**: 127.0.0.1 (for local testing)
     - **Port**: 502
     - **Slave ID**: 1
   - Click `Connect`
3. **Load a Scene**: Open any Factory I/O scene (e.g., "Sorting by Height")
4. **Start Simulation**: Press `F5` to start the simulation

### Running the Sample

```bash
# Navigate to sample directory
cd src/ModbusSample

# Run the sample
dotnet run
```

### Sample Tag Configuration

The sample includes a comprehensive tag configuration (`tags.json`) with common Factory I/O mappings:

```json
{
  "Tags": {
    "Conv1_RunCmd": {
      "type": "coil",
      "address": 0,
      "description": "Conveyor 1 Run Command"
    },
    "Motor1_Speed_Raw": {
      "type": "holding",
      "address": 40010,
      "length": 2,
      "datatype": "float",
      "scale": 1.0,
      "offset": 0.0,
      "unit": "rpm"
    }
  }
}
```

## Common Factory I/O Address Mappings

| Data Type | Factory I/O Range | Description |
|-----------|-------------------|-------------|
| **Coils (Write)** | 0-999 | Digital outputs (actuators, motors, lights) |
| **Discrete Inputs** | 0-999 | Digital inputs (sensors, switches, buttons) |
| **Holding Registers** | 40001-49999 | Analog outputs (setpoints, motor speeds) |
| **Input Registers** | 30001-39999 | Analog inputs (temperatures, pressures, weights) |

### Data Type Notes

- **Boolean values**: Single coil or discrete input
- **16-bit integers**: Single register (1 address)
- **32-bit integers**: Two registers (2 addresses)
- **32-bit floats**: Two registers (2 addresses)

### Endianness Considerations

Many industrial devices use **Big-Endian** byte order:
- **Big-Endian**: Most significant byte first (default for most PLCs)
- **Little-Endian**: Least significant byte first (PC native format)

Word order for 32-bit values:
- **ABCD**: Standard word order (most common)
- **BADC**: Swapped word order (some older PLCs)
- **CDAB**: Byte-swapped within words
- **DCBA**: Completely reversed

## Sample Demonstrations

The console sample includes several demonstrations:

1. **Connection Testing**: Verify connectivity and read system status
2. **Conveyor Control**: Start/stop conveyors and monitor status
3. **Motor Speed Control**: Set motor speeds and read actual values
4. **Sensor Reading**: Read various analog sensors (weight, temperature, pressure)
5. **Continuous Monitoring**: Real-time monitoring of key process variables

## Advanced Features

### TagReader Service

The library includes a high-level `TagReader` service for typed tag operations:

```csharp
var tagReader = new TagReader(modbusClient, logger);

// Read typed values
var temperature = await tagReader.ReadTagAsync("Temperature_1", tempConfig);
var speed = await tagReader.ReadTagAsync("Motor1_Speed", speedConfig);

// Write typed values
await tagReader.WriteTagAsync("Motor1_Speed", speedConfig, 750.0f);

// Bulk operations
var values = await tagReader.ReadTagsAsync(allTags);
```

### Error Handling

The library provides comprehensive error handling:

```csharp
try
{
    var value = await client.ReadHoldingRegistersAsync(40001, 1);
}
catch (ModbusConnectionException ex)
{
    // Handle connection failures
}
catch (ModbusTimeoutException ex)
{
    // Handle timeouts
}
catch (ModbusException ex)
{
    // Handle other Modbus errors
}
```

### Retry Logic

Automatic retry with exponential backoff and jitter:

- **Exponential Backoff**: Delay doubles with each retry
- **Jitter**: Random variance to prevent thundering herd
- **Maximum Delay**: Configurable upper limit
- **Automatic Reconnection**: Handles connection loss gracefully

## Building and Testing

### Prerequisites

- .NET 8 SDK
- Visual Studio 2022 or VS Code
- Factory I/O (optional, for testing)

### Build

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/ModbusClientLib
```

### Run Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Run Sample

```bash
# Run the Factory I/O sample
cd src/ModbusSample
dotnet run
```

## Cross-Platform Testing

### Windows

```bash
dotnet run --project src/ModbusSample
```

### Linux (using Docker)

```bash
# Build and run in container
docker run --rm -it -v ${PWD}:/workspace -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 bash
dotnet run --project src/ModbusSample
```

## Package Installation

```bash
# Install from local build
dotnet pack src/ModbusClientLib
dotnet add package ModbusClientLib --source ./src/ModbusClientLib/bin/Debug/

# Or reference project directly
dotnet add reference ../ModbusClientLib/ModbusClientLib.csproj
```

## Performance Considerations

- **Connection Pooling**: Register client as singleton for connection reuse
- **Batch Operations**: Use `ReadMultiple*` methods for efficiency
- **Timeout Tuning**: Adjust timeouts based on network conditions
- **Retry Configuration**: Balance reliability vs. performance

## Troubleshooting

### Common Issues

1. **Connection Refused**: Check host/port and firewall settings
2. **Timeout Errors**: Increase timeout values or check network latency
3. **Data Conversion**: Verify endianness and word order settings
4. **Permission Denied**: Ensure Factory I/O is running as Modbus server

### Logging

Enable detailed logging for troubleshooting:

```json
{
  "Logging": {
    "LogLevel": {
      "ModbusClientLib": "Debug"
    }
  }
}
```

## License

This project is provided as-is for educational and commercial use. See the source code for implementation details.

## Contributing

This is a complete implementation following industrial automation best practices. The code demonstrates production-ready patterns for Modbus TCP communication in .NET applications.