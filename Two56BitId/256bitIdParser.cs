namespace Two56bitId;

using System.Text.RegularExpressions;

public static partial class Two56BitIdParser
{
    #region Public Methods and Operators

    public static bool TryParse256BitId(string id, out string parsedId)
    {
        parsedId = string.Empty;

        if (Two56BitIdRegex().IsMatch(id) == false)
        {
            return false;
        }

        parsedId = id == "0" ? id : id.TrimStart('0');
        return true;
    }

    #endregion

    #region Methods

    [GeneratedRegex("^[0-9a-fA-F]{1,64}$")]
    private static partial Regex Two56BitIdRegex();

    #endregion
}
