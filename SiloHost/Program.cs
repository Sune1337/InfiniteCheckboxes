using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Orleans.Configuration;

using RedisMessages;
using RedisMessages.Options;

using SiloHost.Utils;

using StackExchange.Redis;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostBuilderContext, serviceCollection) =>
    {
        serviceCollection.Configure<RedisMessagePublisherOptions>(o => o.RedisConnectionString = hostBuilderContext.Configuration.GetConnectionString("ClusterRedis"));
        serviceCollection.AddRedisMessagePublisher();
    })
    .UseOrleans((hostBuilderContext, siloBuilder) =>
    {
        var clusterRedisConnectionString = hostBuilderContext.Configuration.GetConnectionString("ClusterRedis");
        if (string.IsNullOrWhiteSpace(clusterRedisConnectionString))
        {
            throw new Exception("ClusterRedis connection-string is not set.");
        }

        siloBuilder
            .Configure<ClusterOptions>(options =>
                {
                    options.ClusterId = "CheckboxCluster";
                    options.ServiceId = "CheckboxService";
                }
            )
            .Configure<GrainCollectionOptions>(options =>
            {
                options.CollectionAge = TimeSpan.FromMinutes(5);
            })
            .UseRedisClustering(options =>
            {
                options.ConfigurationOptions = ConfigurationOptions.Parse(clusterRedisConnectionString);
                options.ConfigurationOptions.DefaultDatabase = 0;
                options.CreateMultiplexer = clusteringOptions => Task.FromResult<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(clusteringOptions.ConfigurationOptions));
            })
            .AddRedisGrainStorage("CheckboxStore", options =>
            {
                options.ConfigurationOptions = ConfigurationOptions.Parse(clusterRedisConnectionString);
                options.ConfigurationOptions.DefaultDatabase = 1;
                options.CreateMultiplexer = clusteringOptions => Task.FromResult<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(clusteringOptions.ConfigurationOptions));
            })
            .ConfigureEndpoints(TcpPorts.GetNextFreeTcpPort(11111), TcpPorts.GetNextFreeTcpPort(30000))
            .UseDashboard(options => { });
    })
    .UseConsoleLifetime();

using var host = builder.Build();

await host.RunAsync();
