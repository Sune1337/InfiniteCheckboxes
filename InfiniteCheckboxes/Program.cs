using APIKeyAuthentication;

using CheckboxHubv1;
using CheckboxHubv1.Options;

using InfiniteCheckboxes.Utils;

using OpenTelemetry;
using OpenTelemetry.Metrics;

using Orleans.Configuration;
using Orleans.Providers.MongoDB.Configuration;

using Prometheus;

using Serilog;

using WarHubv1;
using WarHubv1.Options;

Serilog.Debugging.SelfLog.Enable(msg => Console.Error.WriteLine(msg));

var builder = WebApplication.CreateBuilder(args);

builder.Host
    .UseSerilog((hostBuilderContext, loggerConfiguration) => { loggerConfiguration.ReadFrom.Configuration(hostBuilderContext.Configuration); });

// Configure options.
builder.Services.Configure<CheckboxObserverOptions>(o => o.RedisConnectionString = builder.Configuration.GetConnectionString("PubSubRedis"));
builder.Services.Configure<UserObserverOptions>(o => o.RedisConnectionString = builder.Configuration.GetConnectionString("PubSubRedis"));
builder.Services.Configure<WarObserverOptions>(o => o.RedisConnectionString = builder.Configuration.GetConnectionString("PubSubRedis"));

// Start the Orleans client before services that might use it.
builder.UseOrleansClient(clientBuilder =>
{
    var clusterMongoDbConnectionString = clientBuilder.Configuration.GetConnectionString("ClusterMongoDb");
    if (string.IsNullOrWhiteSpace(clusterMongoDbConnectionString))
    {
        throw new Exception("ClusterMongoDb connection-string is not set.");
    }

    clientBuilder
        .Configure<ClusterOptions>(options =>
            {
                options.ClusterId = "CheckboxCluster";
                options.ServiceId = "CheckboxService";
            }
        )
        .UseMongoDBClient(clusterMongoDbConnectionString)
        .UseMongoDBClustering(options =>
        {
            options.DatabaseName = "OrleansCluster";
            options.Strategy = MongoDBMembershipStrategy.SingleDocument;
        });
});

// Add services.
builder.Services.AddAPIKeyAuthentication();
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddDefaultExceptionHandler();
builder.Services.AddCheckboxServices();
builder.Services.AddWarObserverService();
builder.Services.AddHsts(options => { options.MaxAge = TimeSpan.FromDays(365); });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Use exception handler.
app.UseExceptionHandler();

// Asp.Net requirements.
app.UseStaticFiles();
app.UseRouting();

if (app.Configuration.GetValue<bool>("UseSerilogRequestLogging"))
{
    // Log HTTP-requests using Serilog.
    app.UseEnrichedSerilogRequestLogging();
}

// Use auth and auth.
app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hubs.
app.MapCheckboxHubv1("/hubs/v1/CheckboxHub");
app.MapWarHubv1("/hubs/v1/WarHub");

// If we use MVC controllers.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action}"
);

app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers["Cache-Control"] = "no-cache, no-store";
        context.Context.Response.Headers["Expires"] = "-1";
    }
});

// Start a metrics server.
var metricsServerPort = app.Configuration.GetValue<int?>("MetricsServerPort");
if (metricsServerPort != null)
{
    // Set up OpenTelemetry with Prometheus
    using var meterProvider = Sdk.CreateMeterProviderBuilder()
        .Build();

    var metricServer = new MetricServer(port: metricsServerPort.Value);
    metricServer.Start();
}

// Run Asp.Net app.
await app.RunAsync();
