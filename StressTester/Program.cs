using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SiloHost.Stressers;

using StressTester.Options;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostBuilderContext, serviceCollection) =>
    {
        serviceCollection.Configure<CheckboxHubStressOptions>(hostBuilderContext.Configuration.GetSection(nameof(CheckboxHubStressOptions)));

        serviceCollection.AddSingleton<CheckboxHubStressService>();
        serviceCollection.AddHostedService<CheckboxHubStressService>(services => services.GetRequiredService<CheckboxHubStressService>());
    })
    .UseConsoleLifetime();

using var host = builder.Build();

await host.RunAsync();
