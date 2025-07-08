namespace CheckboxHubv1;

using CheckboxHubv1.Hubs;

public static class CheckboxHubv1Extensions
{
    #region Public Methods and Operators

    public static void MapCheckboxHubv1(this IEndpointRouteBuilder endpoints, string path)
    {
        endpoints.MapHub<CheckboxHub>(path);
    }

    #endregion
}
