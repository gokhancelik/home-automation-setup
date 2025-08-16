using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModbusClientLib.Abstractions;
using ModbusClientLib.Options;
using ModbusClientLib.Tcp;

namespace ModbusClientLib.Extensions;

/// <summary>
/// Extension methods for registering Modbus client services with dependency injection
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Modbus TCP client services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure client options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddModbusClient(this IServiceCollection services, Action<ModbusClientOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(configureOptions);
        return AddModbusClientCore(services);
    }

    /// <summary>
    /// Adds Modbus TCP client services to the service collection with configuration section binding
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">Configuration section containing Modbus options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddModbusClient(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfigurationSection configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<ModbusClientOptions>(configuration);
        return AddModbusClientCore(services);
    }

    /// <summary>
    /// Adds Modbus TCP client services to the service collection with default options
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddModbusClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        services.Configure<ModbusClientOptions>(_ => { });
        return AddModbusClientCore(services);
    }

    /// <summary>
    /// Adds Modbus TCP client services to the service collection with named options
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="name">The name of the options instance</param>
    /// <param name="configureOptions">Action to configure client options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddModbusClient(this IServiceCollection services, string name, Action<ModbusClientOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.Configure(name, configureOptions);
        
        // Register named client factory
        services.TryAddSingleton<IModbusClientFactory, ModbusClientFactory>();
        
        return services;
    }

    /// <summary>
    /// Core registration logic for Modbus client services
    /// </summary>
    private static IServiceCollection AddModbusClientCore(IServiceCollection services)
    {
        // Register the client as singleton for connection reuse
        services.TryAddSingleton<IIndustrialModbusClient, TcpIndustrialModbusClient>();
        
        // Register validation for options
        services.TryAddSingleton<IValidateOptions<ModbusClientOptions>, ModbusClientOptionsValidator>();
        
        return services;
    }
}

/// <summary>
/// Factory interface for creating named Modbus clients
/// </summary>
public interface IModbusClientFactory
{
    /// <summary>
    /// Creates a Modbus client with the specified options name
    /// </summary>
    /// <param name="name">The name of the options instance</param>
    /// <returns>Configured Modbus client instance</returns>
    IIndustrialModbusClient CreateClient(string name);
}

/// <summary>
/// Factory implementation for creating named Modbus clients
/// </summary>
internal class ModbusClientFactory : IModbusClientFactory
{
    private readonly IOptionsMonitor<ModbusClientOptions> _optionsMonitor;
    private readonly IServiceProvider _serviceProvider;

    public ModbusClientFactory(IOptionsMonitor<ModbusClientOptions> optionsMonitor, IServiceProvider serviceProvider)
    {
        _optionsMonitor = optionsMonitor;
        _serviceProvider = serviceProvider;
    }

    public IIndustrialModbusClient CreateClient(string name)
    {
        var options = _optionsMonitor.Get(name);
        var logger = _serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TcpIndustrialModbusClient>>();
        
        return new TcpIndustrialModbusClient(Microsoft.Extensions.Options.Options.Create(options), logger);
    }
}

/// <summary>
/// Validator for Modbus client options
/// </summary>
internal class ModbusClientOptionsValidator : IValidateOptions<ModbusClientOptions>
{
    public ValidateOptionsResult Validate(string? name, ModbusClientOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (ArgumentException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}