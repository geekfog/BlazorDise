using BlazorDise.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

var builder = FunctionsApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment()) // If DOTNET_ENVIRONMENT = "Development"?
{
    builder.Configuration.AddUserSecrets<Program>();
}

// Retrieve the Azure Storage connection string from configuration
var storageConnectionString = builder.Configuration[Constants.ConfigStorageAccount];

// Configure Serilog with custom output template
var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: Constants.SerilogTemplateNoDate)
    .WriteTo.AzureTableStorage(connectionString: storageConnectionString, storageTableName: Constants.SeriogAzureTableName, restrictedToMinimumLevel: LogEventLevel.Information)
    .Enrich.WithProperty("Application", "fn")
    .Enrich.WithProperty("InstanceId", Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? Environment.MachineName)
    .Enrich.WithProperty("AspNetCoreEnvironment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown")
    .Enrich.WithEnvironmentName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Region", Environment.GetEnvironmentVariable("REGION_NAME") ?? "Unknown");

if (builder.Environment.IsDevelopment())
{
    // Set log file path to be off the root folder (not under bin)
    var logFilePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", Constants.SerilogLogFilePath);
    logFilePath = Path.GetFullPath(logFilePath);
    loggerConfig = loggerConfig.WriteTo.File(path: logFilePath, outputTemplate: Constants.SerilogTemplate, rollingInterval: RollingInterval.Day, retainedFileCountLimit: Constants.SerilogRollingIntervalDays);
}

Log.Logger = loggerConfig.CreateLogger();
   
// Register Serilog as the logging provider
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
    loggingBuilder.AddSerilog();
});

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
