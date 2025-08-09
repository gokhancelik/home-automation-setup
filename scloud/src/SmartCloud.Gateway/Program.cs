using SmartCloud.Core.Interfaces;
using SmartCloud.DataIngestion.Services;
using SmartCloud.Storage.Services;
using SmartCloud.Analytics.Services;
using SmartCloud.Gateway.Services;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/gateway-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Services.AddSerilog();

// Register services
builder.Services.AddSingleton<IDataIngestionService, MqttDataIngestionService>();
builder.Services.AddSingleton<IDataStorageService, InfluxDbStorageService>();
builder.Services.AddSingleton<IPredictiveAnalyticsService, PredictiveAnalyticsService>();

// Register the main gateway worker
builder.Services.AddHostedService<GatewayWorker>();

// Configure for Windows Service if needed
if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService();
}

var host = builder.Build();

try
{
    Log.Information("Starting SmartCloud Gateway");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Gateway terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
