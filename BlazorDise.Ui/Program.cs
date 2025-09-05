using BlazorDise.Shared;
using BlazorDise.Ui.Components;
using BlazorDise.Ui.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.SignalR.Management;
using Serilog;
using Serilog.Events;

namespace BlazorDise.Ui;

public class Program
{
    public static void Main(string[] args)
    {
        // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // Build Services
        // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        var builder = WebApplication.CreateBuilder(args);

        // ~~~~~~~ [ Configure Logging ] ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        var storageConnectionString = builder.Configuration[Constants.ConfigStorageAccount]; // Retrieve the Azure Storage connection string from configuration

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: Constants.SerilogTemplate)
            .WriteTo.AzureTableStorage(connectionString: storageConnectionString, storageTableName: Constants.SeriogAzureTableName, restrictedToMinimumLevel: LogEventLevel.Information)
            .Enrich.WithProperty("Application", "ui")
            .Enrich.WithProperty("InstanceId", Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") ?? Environment.MachineName)
            .Enrich.WithProperty("AspNetCoreEnvironment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown")
            .Enrich.WithEnvironmentName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Region", Environment.GetEnvironmentVariable("REGION_NAME") ?? "Unknown");

        if (builder.Environment.IsDevelopment())
            loggerConfig.WriteTo.File(path: Constants.SerilogLogFilePath, outputTemplate: Constants.SerilogTemplate, rollingInterval: RollingInterval.Day, retainedFileCountLimit: Constants.SerilogRollingIntervalDays);

        Log.Logger = loggerConfig.CreateLogger();
        builder.Host.UseSerilog(); // Replace default logging with Serilog

        // ~~~~~~~ [ User Secrets Configuration ] ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        if (builder.Environment.IsDevelopment())
            builder.Configuration.AddUserSecrets<Program>();

        // ~~~~~~~ [ Dependency Injections ] ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        builder.Services.AddHttpClient(Constants.SignalRHttpName); // HttpClient service for SignalR negotiation
        builder.Services.AddScoped<SignalRHttpClientProvider>();

        // ~~~~~~~ [ Add Services to the Container ] ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // Build Application
        // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        var app = builder.Build();

        if (!app.Environment.IsDevelopment()) // Configure the HTTP request pipeline.
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts(); // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        }

        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        // ~~~~~~~ [ SignalR Endpoint ] ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // Anonymous is required if OAuth2 is leveraged on the site (it isn't in this case, but demonstrates how to set up if it is)
        // Currently can't pass in the access token since this is running server-side, not client-side or on behalf of the client
        app.MapGet($"/{Constants.SignalREndpoint}", [AllowAnonymous] async (IConfiguration config) =>
        {
            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(o => o.ConnectionString = config[Constants.ConfigSignalRAccount])
                .BuildServiceManager();

            var hubContext = await serviceManager.CreateHubContextAsync(Constants.SignalRHubName, CancellationToken.None);
            var negotiateResponse = await hubContext.NegotiateAsync();
            return Results.Json(negotiateResponse);
        });

        app.Run();
    }
}