namespace SiloHost.Stressers;

using System.Security.Cryptography;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using StressTester.Options;

public class CheckboxHubStressService : IHostedService
{
    #region Fields

    private readonly string _checkboxHubUrl;
    private readonly ILogger<CheckboxHubStressService> _logger;
    private readonly IOptions<CheckboxHubStressOptions> _options;
    private readonly CancellationTokenSource _testCancellationTokenSource = new();

    private Task? _checkboxHubTestTask;

    #endregion

    #region Constructors and Destructors

    public CheckboxHubStressService(ILogger<CheckboxHubStressService> logger, IOptions<CheckboxHubStressOptions> options)
    {
        _logger = logger;
        _options = options;

        if (string.IsNullOrWhiteSpace(_options.Value.CheckboxHubUrl))
        {
            throw new ArgumentNullException(nameof(_options.Value.CheckboxHubUrl), "CheckboxHubUrl is null or empty.");
        }

        _checkboxHubUrl = _options.Value.CheckboxHubUrl;
    }

    #endregion

    #region Public Methods and Operators

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _checkboxHubTestTask = CheckboxHubTest();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _testCancellationTokenSource.CancelAsync();

        if (_checkboxHubTestTask != null)
        {
            await _checkboxHubTestTask;
        }
    }

    #endregion

    #region Methods

    private static string Generate256BitHex()
    {
        // Create a byte array to hold 32 bytes (256 bits)
        var bytes = new byte[32];

        // Fill it with cryptographically strong random bytes
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        // Convert to hex string
        return Convert.ToHexStringLower(bytes).TrimStart('0');
    }

    private async Task CheckboxHubTest()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_checkboxHubUrl), options =>
            {
                options.HttpMessageHandlerFactory = _ =>
                {
                    var handler = new HttpClientHandler();
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    return handler;
                };
            })
            .WithAutomaticReconnect()
            .Build();

        try
        {
            await connection.StartAsync();

            var pageId = Generate256BitHex();
            while (!_testCancellationTokenSource.IsCancellationRequested)
            {
                // Generate a random page id.
                var randomIndex = new Random().Next(0, 4096);
                var randomValue = new Random().Next(0, 2);

                await connection.InvokeAsync("SetCheckbox", pageId, randomIndex, randomValue);
            }
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while testing CheckboxHub.");
        }

        finally
        {
            await connection.DisposeAsync();
        }
    }

    #endregion
}
