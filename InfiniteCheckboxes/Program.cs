using InfiniteCheckboxes.Utils;

using Orleans.Configuration;

using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDefaultExceptionHandler();
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
