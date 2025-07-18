using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenTelemetry;
using OpenTelemetry.Metrics;

using Orleans.Configuration;
using Orleans.Providers.MongoDB.Configuration;

using Prometheus;

using RedisMessages;
using RedisMessages.Options;

using Serilog;

using SiloHost.Utils;

Serilog.Debugging.SelfLog.Enable(msg => Console.Error.WriteLine(msg));

var builder = Host.CreateDefaultBuilder(args)
    .UseSerilog((hostBuilderContext, loggerConfiguration) =>
    {
        loggerConfiguration.ReadFrom.Configuration(hostBuilderContext.Configuration);
    })
    .ConfigureServices((hostBuilderContext, serviceCollection) =>
    {
        serviceCollection.Configure<RedisMessagePublisherOptions>(o => o.RedisConnectionString = hostBuilderContext.Configuration.GetConnectionString("PubSubRedis"));
        serviceCollection.AddRedisMessagePublishers();
    })
    .UseOrleans((hostBuilderContext, siloBuilder) =>
    {
        var clusterMongoDbConnectionString = hostBuilderContext.Configuration.GetConnectionString("ClusterMongoDb");
        if (string.IsNullOrWhiteSpace(clusterMongoDbConnectionString))
        {
            throw new Exception("ClusterMongoDb connection-string is not set.");
        }

        siloBuilder
            .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "CheckboxCluster";
                    options.ServiceId = "CheckboxService";
                }
            )
            .Configure<ClusterMembershipOptions>(options =>
            {
                // Reduce how long we keep dead silos around
                options.DefunctSiloExpiration = TimeSpan.FromMinutes(2);

                // How often to cleanup defunct (dead) silos
                options.DefunctSiloCleanupPeriod = TimeSpan.FromMinutes(1);
            })
            .Configure<GrainCollectionOptions>(options => { options.CollectionAge = TimeSpan.FromMinutes(5); })
            .UseMongoDBClient(clusterMongoDbConnectionString)
            .UseMongoDBClustering(options =>
            {
                options.DatabaseName = "OrleansCluster";
                options.Strategy = MongoDBMembershipStrategy.SingleDocument;
            })
            .AddMongoDBGrainStorage("CheckboxStore", options =>
            {
                options.DatabaseName = "CheckboxGrains";
                options.CreateShardKeyForCosmos = false;
            })
            .AddMongoDBGrainStorage("WarStore", options =>
            {
                options.DatabaseName = "WarGrains";
                options.CreateShardKeyForCosmos = false;
            })
            .ConfigureEndpoints(TcpPorts.GetNextFreeTcpPort(11111), TcpPorts.GetNextFreeTcpPort(30000))
            .UseDashboard(options => { });

        var podNamespace = hostBuilderContext.Configuration.GetValue<string>("POD_NAMESPACE");
        if (string.IsNullOrEmpty(podNamespace) == false)
        {
            siloBuilder.UseKubernetesHosting();
        }
    })
    .UseConsoleLifetime();

using var host = builder.Build();

// Start a metrics server.
var configuration = host.Services.GetRequiredService<IConfiguration>();
var metricsServerPort = configuration.GetValue<int?>("MetricsServerPort");
if (metricsServerPort != null)
{
    // Set up OpenTelemetry with Prometheus
    using var meterProvider = Sdk.CreateMeterProviderBuilder()
        .Build();

    var metricServer = new MetricServer(port: metricsServerPort.Value);
    metricServer.Start();
}

await host.RunAsync();

// Flush logs.
await Log.CloseAndFlushAsync();
