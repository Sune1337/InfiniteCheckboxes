using CheckboxHubv1;
using CheckboxHubv1.Options;

using InfiniteCheckboxes.Utils;

using Orleans.Configuration;
using Orleans.Providers.MongoDB.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure options.
builder.Services.Configure<CheckboxObserverOptions>(o => o.RedisConnectionString = builder.Configuration.GetConnectionString("PubSubRedis"));

// ADd services.
builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddDefaultExceptionHandler();
builder.Services.AddCheckboxObserverService();
builder.Services.AddHsts(options => { options.MaxAge = TimeSpan.FromDays(365); });

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
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
