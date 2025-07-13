namespace CheckboxHubv1.Hubs;

using System.Text.RegularExpressions;

using CheckboxHubv1.CheckboxObserver;

using GrainInterfaces;

using Microsoft.AspNetCore.SignalR;

using Two56bitId;

public partial class CheckboxHub : Hub
{
    #region Fields

    private readonly ICheckboxObserverManager _checkboxObserverManager;
    private readonly IGrainFactory _grainFactory;

    #endregion

    #region Constructors and Destructors

    public CheckboxHub(IGrainFactory grainFactory, ICheckboxObserverManager checkboxObserverManager)
    {
        _grainFactory = grainFactory;
        _checkboxObserverManager = checkboxObserverManager;
    }

    #endregion

    #region Properties

    private HashSet<string>? CheckboxIds => Context.Items["CheckboxIds"] as HashSet<string>;

    #endregion

    #region Public Methods and Operators

    public async Task<byte[]?> CheckboxesSubscribe(string base64Id)
    {
        if (base64Id.TryParse256BitBase64Id(out var parsedId) == false)
        {
            throw new ArgumentException("Invalid checkbox page id.", nameof(base64Id));
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"{HubGroups.CheckboxGroupPrefix}_{parsedId}");
        await _checkboxObserverManager.SubscribeAsync(parsedId);

        // Remember that the current client subscribes to this checkbox-page.
        CheckboxIds?.Add(parsedId);

        // Get the initial state of checkboxes.
        var checkboxGrain = _grainFactory.GetGrain<ICheckboxGrain>(parsedId);
        return await checkboxGrain.GetCheckboxes();
    }

    public async Task CheckboxesUnsubscribe(string base64Id)
    {
        if (base64Id.TryParse256BitBase64Id(out var parsedId) == false)
        {
            throw new ArgumentException("Invalid checkbox page id.", nameof(base64Id));
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{HubGroups.CheckboxGroupPrefix}_{parsedId}");
        await _checkboxObserverManager.UnsubscribeAsync(parsedId);

        // The current connection no longer subscribes to the checkbox-page.
        CheckboxIds?.Remove(parsedId);
    }

    public override Task OnConnectedAsync()
    {
        // Create a list to keep track of which checkbox-pages the connection subscribes to.
        Context.Items.Add("CheckboxIds", new HashSet<string>());
        return Task.CompletedTask;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (CheckboxIds != null)
        {
            foreach (var id in CheckboxIds)
            {
                try
                {
                    await _checkboxObserverManager.UnsubscribeAsync(id);
                }
                catch
                {
                    // ignored
                }
            }

            // Remove the list of checkbox-pages. 
            CheckboxIds.Clear();
            Context.Items.Remove("CheckboxIds");
        }
    }

    public async Task<string?> SetCheckbox(string base64Id, int index, byte value)
    {
        if (base64Id.TryParse256BitBase64Id(out var parsedId) == false)
        {
            throw new ArgumentException("Invalid checkbox page id.", nameof(base64Id));
        }

        try
        {
            // Set state of checkbox.
            var checkboxGrain = _grainFactory.GetGrain<ICheckboxGrain>(parsedId);
            await checkboxGrain.SetCheckbox(index, value);
        }

        catch (Exception ex)
        {
            return ex.Message;
        }

        return null;
    }

    #endregion

    #region Methods

    [GeneratedRegex("^[0-9a-fA-F]{1..64}$")]
    private static partial Regex CheckboxPageIdRegex();

    #endregion
}
