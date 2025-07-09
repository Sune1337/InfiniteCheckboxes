using CheckboxHubv1;
using CheckboxHubv1.Options;

using InfiniteCheckboxes.Utils;

using Orleans.Configuration;

using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Configure options.
builder.Services.Configure<CheckboxObserverOptions>(o => o.RedisConnectionString = builder.Configuration.GetConnectionString("ClusterRedis"));

// ADd services.
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddDefaultExceptionHandler();
builder.Services.AddCheckboxObserverService();

builder.UseOrleansClient(clientBuilder =>
{
    var clusterRedisConnectionString = builder.Configuration.GetConnectionString("ClusterRedis");
    if (string.IsNullOrWhiteSpace(clusterRedisConnectionString))
    {
        throw new Exception("ClusterRedis connection-string is not set.");
    }

    clientBuilder
        .Configure<ClusterOptions>(options =>
            {
                options.ClusterId = "CheckboxCluster";
                options.ServiceId = "CheckboxService";
            }
        )
        .UseRedisClustering(options =>
        {
            options.ConfigurationOptions = ConfigurationOptions.Parse(clusterRedisConnectionString);
            options.ConfigurationOptions.DefaultDatabase = 0;
            options.CreateMultiplexer = clusteringOptions => Task.FromResult<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(clusteringOptions.ConfigurationOptions));
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    builder.Services.AddHsts(options => { options.MaxAge = TimeSpan.FromDays(365); });
}

// Use exception handler.
app.UseExceptionHandler();

app.UseStaticFiles();
app.UseRouting();

app.MapCheckboxHubv1("/hubs/v1/CheckboxHub");

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

app.Run();
