using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModbusClientLib.Extensions;
using ModbusSample.Models;
using ModbusSample.Services;
using System.Text.Json;

namespace ModbusSample;

/// <summary>
/// Main application class demonstrating Modbus TCP client usage with Factory I/O
/// </summary>
public class Program
{
    private static readonly CancellationTokenSource _cancellationTokenSource = new();
    
    public static async Task Main(string[] args)
    {
        // Set up console cancellation
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _cancellationTokenSource.Cancel();
            Console.WriteLine("\nShutdown requested...");
        };

        try
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            // Build service provider
            var services = new ServiceCollection()
                .AddLogging(builder => builder
                    .AddConfiguration(configuration.GetSection("Logging"))
                    .AddConsole())
                .AddModbusClient(configuration.GetSection("Modbus"))
                .AddSingleton<TagReader>()
                .BuildServiceProvider();

            var logger = services.GetRequiredService<ILogger<Program>>();
            
            logger.LogInformation("=== Factory I/O Modbus TCP Client Demo ===");
            logger.LogInformation("Starting Modbus TCP client demonstration...");

            // Load tag configuration
            var tags = await LoadTagConfigurationAsync(logger);
            if (tags.Count == 0)
            {
                logger.LogError("No tags loaded. Check tags.json file.");
                return;
            }

            logger.LogInformation("Loaded {TagCount} tags from configuration", tags.Count);

            // Run the demonstration
            await RunDemonstrationAsync(services, tags, _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Application cancelled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Application failed: {ex.Message}");
            Console.WriteLine($"Details: {ex}");
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// Loads tag configuration from tags.json file
    /// </summary>
    private static async Task<Dictionary<string, TagConfig>> LoadTagConfigurationAsync(ILogger logger)
    {
        try
        {
            var tagConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "tags.json");
            if (!File.Exists(tagConfigPath))
            {
                logger.LogError("Tag configuration file not found: {Path}", tagConfigPath);
                return new Dictionary<string, TagConfig>();
            }

            var json = await File.ReadAllTextAsync(tagConfigPath);
            var tagData = JsonSerializer.Deserialize<JsonElement>(json);
            
            var tags = new Dictionary<string, TagConfig>();
            
            if (tagData.TryGetProperty("Tags", out var tagsElement))
            {
                foreach (var tagProperty in tagsElement.EnumerateObject())
                {
                    var tagConfig = JsonSerializer.Deserialize<TagConfig>(tagProperty.Value.GetRawText());
                    if (tagConfig != null)
                    {
                        tagConfig.Validate();
                        tags[tagProperty.Name] = tagConfig;
                        logger.LogDebug("Loaded tag: {TagName} -> {Address} ({Type})", 
                            tagProperty.Name, tagConfig.Address, tagConfig.Type);
                    }
                }
            }

            return tags;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load tag configuration");
            return new Dictionary<string, TagConfig>();
        }
    }

    /// <summary>
    /// Runs the main demonstration workflow
    /// </summary>
    private static async Task RunDemonstrationAsync(IServiceProvider services, Dictionary<string, TagConfig> tags, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        var tagReader = services.GetRequiredService<TagReader>();

        try
        {
            logger.LogInformation("Connecting to Modbus TCP server...");

            // Test connection by reading system status
            if (tags.TryGetValue("System_Status", out var systemStatusTag))
            {
                var systemStatus = await tagReader.ReadTagAsync("System_Status", systemStatusTag, cancellationToken);
                logger.LogInformation("System Status: {Status}", TagReader.FormatTagValue(systemStatusTag, systemStatus));
            }

            // Demonstrate conveyor control
            await DemonstrateConveyorControlAsync(tagReader, tags, logger, cancellationToken);

            // Demonstrate motor speed control
            await DemonstrateMotorControlAsync(tagReader, tags, logger, cancellationToken);

            // Demonstrate sensor reading
            await DemonstrateSensorReadingAsync(tagReader, tags, logger, cancellationToken);

            // Continuous monitoring loop
            await ContinuousMonitoringAsync(tagReader, tags, logger, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Operation cancelled by user");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during demonstration");
            throw;
        }
    }

    /// <summary>
    /// Demonstrates conveyor control operations
    /// </summary>
    private static async Task DemonstrateConveyorControlAsync(TagReader tagReader, Dictionary<string, TagConfig> tags, ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogInformation("=== Conveyor Control Demonstration ===");

        if (!tags.TryGetValue("Conv1_RunCmd", out var conv1RunTag) ||
            !tags.TryGetValue("Conv1_Running", out var conv1StatusTag))
        {
            logger.LogWarning("Conveyor tags not found in configuration");
            return;
        }

        try
        {
            // Start conveyor 1
            logger.LogInformation("Starting Conveyor 1...");
            await tagReader.WriteTagAsync("Conv1_RunCmd", conv1RunTag, true, cancellationToken);
            
            // Wait and check status
            await Task.Delay(2000, cancellationToken);
            
            var isRunning = await tagReader.ReadTagAsync("Conv1_Running", conv1StatusTag, cancellationToken);
            logger.LogInformation("Conveyor 1 Status: {Status}", TagReader.FormatTagValue(conv1StatusTag, isRunning));

            // Wait a bit then stop
            await Task.Delay(5000, cancellationToken);
            
            logger.LogInformation("Stopping Conveyor 1...");
            await tagReader.WriteTagAsync("Conv1_RunCmd", conv1RunTag, false, cancellationToken);
            
            await Task.Delay(2000, cancellationToken);
            isRunning = await tagReader.ReadTagAsync("Conv1_Running", conv1StatusTag, cancellationToken);
            logger.LogInformation("Conveyor 1 Status: {Status}", TagReader.FormatTagValue(conv1StatusTag, isRunning));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during conveyor control demonstration");
        }
    }

    /// <summary>
    /// Demonstrates motor speed control
    /// </summary>
    private static async Task DemonstrateMotorControlAsync(TagReader tagReader, Dictionary<string, TagConfig> tags, ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogInformation("=== Motor Speed Control Demonstration ===");

        if (!tags.TryGetValue("Motor1_Speed_Raw", out var speedSetpointTag) ||
            !tags.TryGetValue("Motor1_Speed_Actual", out var speedActualTag))
        {
            logger.LogWarning("Motor speed tags not found in configuration");
            return;
        }

        try
        {
            var targetSpeeds = new[] { 100.0f, 250.0f, 500.0f, 750.0f, 1000.0f };

            foreach (var targetSpeed in targetSpeeds)
            {
                if (cancellationToken.IsCancellationRequested) break;

                logger.LogInformation("Setting motor speed to {Speed} rpm", targetSpeed);
                await tagReader.WriteTagAsync("Motor1_Speed_Raw", speedSetpointTag, targetSpeed, cancellationToken);

                // Wait for motor to respond
                await Task.Delay(3000, cancellationToken);

                var actualSpeed = await tagReader.ReadTagAsync("Motor1_Speed_Actual", speedActualTag, cancellationToken);
                logger.LogInformation("Motor 1 Actual Speed: {Speed}", TagReader.FormatTagValue(speedActualTag, actualSpeed));
            }

            // Reset to zero
            logger.LogInformation("Resetting motor speed to 0 rpm");
            await tagReader.WriteTagAsync("Motor1_Speed_Raw", speedSetpointTag, 0.0f, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during motor control demonstration");
        }
    }

    /// <summary>
    /// Demonstrates reading various sensor values
    /// </summary>
    private static async Task DemonstrateSensorReadingAsync(TagReader tagReader, Dictionary<string, TagConfig> tags, ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogInformation("=== Sensor Reading Demonstration ===");

        var sensorTags = new[] { "Scale_Weight_Raw", "Temperature_1", "Pressure_1", "Production_Count" };

        foreach (var tagName in sensorTags)
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (tags.TryGetValue(tagName, out var tagConfig))
            {
                try
                {
                    var value = await tagReader.ReadTagAsync(tagName, tagConfig, cancellationToken);
                    var description = tagConfig.Description ?? tagName;
                    logger.LogInformation("{Description}: {Value}", description, TagReader.FormatTagValue(tagConfig, value));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to read sensor {TagName}", tagName);
                }
            }
            else
            {
                logger.LogWarning("Sensor tag {TagName} not found in configuration", tagName);
            }

            await Task.Delay(1000, cancellationToken);
        }
    }

    /// <summary>
    /// Runs continuous monitoring of key tags
    /// </summary>
    private static async Task ContinuousMonitoringAsync(TagReader tagReader, Dictionary<string, TagConfig> tags, ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogInformation("=== Continuous Monitoring (Press Ctrl+C to stop) ===");

        var monitoringTags = tags
            .Where(kvp => new[] { "Conv1_Running", "Motor1_Speed_Actual", "Scale_Weight_Raw", "Temperature_1", "Emergency_Stop" }
                .Contains(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (monitoringTags.Count == 0)
        {
            logger.LogWarning("No monitoring tags found");
            return;
        }

        logger.LogInformation("Monitoring {TagCount} tags every 5 seconds...", monitoringTags.Count);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var values = await tagReader.ReadTagsAsync(monitoringTags, cancellationToken);
                
                Console.WriteLine($"\n--- Monitoring Update [{DateTime.Now:HH:mm:ss}] ---");
                foreach (var (tagName, value) in values)
                {
                    if (monitoringTags.TryGetValue(tagName, out var tagConfig))
                    {
                        var description = tagConfig.Description ?? tagName;
                        var formattedValue = TagReader.FormatTagValue(tagConfig, value);
                        Console.WriteLine($"  {description}: {formattedValue}");
                    }
                }

                await Task.Delay(5000, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during continuous monitoring");
                await Task.Delay(5000, cancellationToken);
            }
        }

        logger.LogInformation("Continuous monitoring stopped");
    }
}
