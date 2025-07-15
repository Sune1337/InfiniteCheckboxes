using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using NBomber.CSharp;
using NBomber.Sinks.Timescale;

using StressTester.Options;
using StressTester.Scenarios;

// This reporting sink will save stats data into TimescaleDB.
var timescaleDbSink = new TimescaleDbSink();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(o => { o.AddConsole(); })
    .ConfigureServices((hostContext, services) =>
    {
        // Register your configuration
        services.Configure<CheckboxHubStressOptions>(hostContext.Configuration.GetSection("CheckboxHubStressOptions"));

        // Register your scenarios as services
        services.AddSingleton<SetCheckBoxesOnRandomPage>();

        // Register any other services you need
        services.AddSingleton<HttpClient>();
    })
    .Build();

// Get your scenario class from the service provider
var setCheckBoxesScenario = host.Services.GetRequiredService<SetCheckBoxesOnRandomPage>();

// Run NBomber.
var nodeStats = NBomberRunner
    .RegisterScenarios(
        setCheckBoxesScenario.CreateScenario()
            .WithoutWarmUp()
            .WithLoadSimulations(
                Simulation.InjectRandom(
                    interval: TimeSpan.FromSeconds(1),
                    during: TimeSpan.FromHours(23),
                    minRate: 10,
                    maxRate: 20
                )
                
                // Use KeepConstant 1 for debugging.
                // Simulation.KeepConstant(1, TimeSpan.FromMinutes(5))
            )
    )
    .LoadInfraConfig("infra-config.json")
    .WithReportingSinks(timescaleDbSink)
    .WithTestSuite("InfiniteCheckboxes")
    .WithTestName("Bomb infinite-checkboxes")
    .Run();
