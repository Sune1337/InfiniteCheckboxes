namespace StressTester.Scenarios;

using System.Security.Cryptography;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

using NBomber.Contracts;
using NBomber.CSharp;

using StressTester.Options;

public class SetCheckBoxesOnRandomPage
{
    #region Static Fields

    private static readonly byte[] SharedRandomPageId = Generate256BitHex();

    #endregion

    #region Fields

    private readonly string _checkboxHubUrl;

    #endregion

    #region Constructors and Destructors

    public SetCheckBoxesOnRandomPage(IOptions<CheckboxHubStressOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.CheckboxHubUrl))
        {
            throw new ArgumentNullException(nameof(options.Value.CheckboxHubUrl), "CheckboxHubUrl is null or empty.");
        }

        _checkboxHubUrl = options.Value.CheckboxHubUrl;
    }

    #endregion

    #region Public Methods and Operators

    public ScenarioProps CreateScenario()
    {
        var writeSharedPageIdToLog = true;

        Console.WriteLine($"Shared page: {Convert.ToHexStringLower(SharedRandomPageId)}");

        return Scenario.Create(nameof(SetCheckBoxesOnRandomPage), async context =>
        {
            var useSharedPageId = Random.Shared.Next(0, 10) == 0;
            var pageId = useSharedPageId ? SharedRandomPageId : Generate256BitHex();

            if (writeSharedPageIdToLog)
            {
                writeSharedPageIdToLog = false;
                context.Logger.Information("Shared page: {sharedPageId}", SharedRandomPageId);
            }

            // Open connection to CheckboxHub.
            var connection = await Step.Run("ConnectCheckboxHub", context, async () =>
            {
                var connection = new HubConnectionBuilder()
                    .WithUrl(new Uri(_checkboxHubUrl), options => { options.HttpMessageHandlerFactory = HttpMessageHandlerFactory; })
                    .Build();

                await connection.StartAsync();

                return Response.Ok(connection);
            });

            // Subscribe to the page.
            await Step.Run("CheckboxesSubscribe", context, async () =>
            {
                await connection.Payload.Value.InvokeAsync<string>("CheckboxesSubscribe", pageId);
                return Response.Ok();
            });

            // Register callback when page is updated.
            connection.Payload.Value.On("CheckboxesUpdate", async (string id, int[][] checkBoxes) => { await Step.Run("CheckboxesUpdated", context, () => Task.FromResult(Response.Ok())); });

            // Generate a random page id.
            var numberOfSteps = Random.Shared.Next(1, 100);
            for (int i = 0; i < numberOfSteps; i++)
            {
                await Step.Run("SetRandomCheckbox", context, async () =>
                {
                    var randomIndex = new Random().Next(0, 4096);
                    var randomValue = new Random().Next(0, 2);
                    var result = await connection.Payload.Value.InvokeAsync<string>("SetCheckbox", pageId, randomIndex, randomValue);

                    if (string.IsNullOrEmpty(result) == false)
                    {
                        throw new Exception(result);
                    }

                    return Response.Ok();
                });

                await Task.Delay(Random.Shared.Next(200, 400));
            }

            // Unsubscribe to the page.
            await Step.Run("CheckboxesUnsubscribe", context, async () =>
            {
                await connection.Payload.Value.InvokeAsync<string>("CheckboxesUnsubscribe", pageId);
                return Response.Ok();
            });

            // Close the SignalR connection.
            await connection.Payload.Value.DisposeAsync();

            return Response.Ok();
        });
    }

    #endregion

    #region Methods

    private static byte[] Generate256BitHex()
    {
        // Create a byte array to hold 32 bytes (256 bits)
        var bytes = new byte[32];

        // Fill it with cryptographically strong random bytes
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        return bytes;
    }

    private static HttpMessageHandler HttpMessageHandlerFactory(HttpMessageHandler arg)
    {
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
    }

    #endregion
}
