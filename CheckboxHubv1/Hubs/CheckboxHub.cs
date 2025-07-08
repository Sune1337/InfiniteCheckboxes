namespace CheckboxHubv1.Hubs;

using System.Text.RegularExpressions;

using GrainInterfaces;

using Microsoft.AspNetCore.SignalR;

using Two56bitId;

public partial class CheckboxHub : Hub
{
    #region Fields

    private readonly IGrainFactory _grainFactory;

    #endregion

    #region Constructors and Destructors

    public CheckboxHub(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    #endregion

    #region Public Methods and Operators

    public async Task<byte[]> CheckboxesSubscribe(string id)
    {
        if (Two56BitIdParser.TryParse256BitId(id, out var parsedId) == false)
        {
            throw new ArgumentException("Invalid checkbox page id.", nameof(id));
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"{HubGroups.CheckboxGroupPrefix}_{parsedId}");

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
