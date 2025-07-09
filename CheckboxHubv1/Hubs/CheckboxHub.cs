namespace CheckboxHubv1.Hubs;

using System.Text.RegularExpressions;

using CheckboxHubv1.CheckboxObservers;

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

    private HashSet<string>? ComboboxIds => Context.Items["ComboboxIds"] as HashSet<string>;

    #endregion

    #region Public Methods and Operators

    public async Task<byte[]> CheckboxesSubscribe(string id)
    {
        if (Two56BitIdParser.TryParse256BitId(id, out var parsedId) == false)
        {
            throw new ArgumentException("Invalid checkbox page id.", nameof(id));
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"{HubGroups.CheckboxGroupPrefix}_{parsedId}");
        await _checkboxObserverManager.SubscribeAsync(parsedId);

        // Remember that the current client subscribes to this checkbox-page.
        ComboboxIds?.Add(parsedId);

        // Get the initial state of checkboxes.
        var checkboxGrain = _grainFactory.GetGrain<ICheckboxGrain>(id);
        return await checkboxGrain.GetCheckboxes();
    }

    public async Task CheckboxesUnsubscribe(string id)
    {
        if (Two56BitIdParser.TryParse256BitId(id, out var parsedId) == false)
        {
            throw new ArgumentException("Invalid checkbox page id.", nameof(id));
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{HubGroups.CheckboxGroupPrefix}_{parsedId}");
        await _checkboxObserverManager.UnsubscribeAsync(parsedId);

        // The current connection no longer subscribes to the checkbox-page.
        ComboboxIds?.Remove(parsedId);
    }

    public override async Task OnConnectedAsync()
    {
        // Create a list to keep track of which checkbox-pages the connection subscribes to.
        Context.Items.Add("ComboboxIds", new HashSet<string>());
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ComboboxIds != null)
        {
            foreach (var id in ComboboxIds)
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
            ComboboxIds.Clear();
            Context.Items.Remove("ComboboxIds");
        }
    }

    public async Task SetCheckbox(string id, int index, byte value)
    {
        if (Two56BitIdParser.TryParse256BitId(id, out var parsedId) == false)
        {
            throw new ArgumentException("Invalid checkbox page id.", nameof(id));
        }

        // Set state of checkbox.
        var checkboxGrain = _grainFactory.GetGrain<ICheckboxGrain>(parsedId);
        await checkboxGrain.SetCheckbox(index, value);
    }

    #endregion

    #region Methods

    [GeneratedRegex("^[0-9a-fA-F]{1..64}$")]
    private static partial Regex CheckboxPageIdRegex();

    #endregion
}
