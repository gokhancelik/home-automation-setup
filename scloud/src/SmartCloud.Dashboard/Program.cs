using SmartCloud.Core.Interfaces;
using SmartCloud.Storage.Services;
using SmartCloud.Analytics.Services;
using SmartCloud.Dashboard.Hubs;
using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();

// Add Blazorise
builder.Services
    .AddBlazorise(options =>
    {
        options.Immediate = true;
    })
    .AddBootstrap5Providers()
    .AddFontAwesomeIcons();

// Add custom services
builder.Services.AddSingleton<IDataStorageService, InfluxDbStorageService>();
// Temporarily disabled due to InfluxDB 1.x compatibility
// builder.Services.AddSingleton<IPredictiveAnalyticsService, PredictiveAnalyticsService>();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseCors();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Map SignalR hubs
app.MapHub<DashboardHub>("/dashboardhub");

app.Run();
