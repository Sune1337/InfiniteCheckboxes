namespace APIKeyAuthentication;

using APIKeyAuthentication.Options;

public static class APIKeyAuthenticationExtensions
{
    #region Public Methods and Operators

    public static void AddAPIKeyAuthentication(this IServiceCollection services, Action<APIKeyAuthenticationOptions>? configureOptions = null)
    {
        // Add api-key authentication.
        services
            .AddAuthentication()
            .AddScheme<APIKeyAuthenticationOptions, APIKeyAuthenticationHandler>(APIKeyConstants.SchemeName, configureOptions);
    }

    #endregion
}
