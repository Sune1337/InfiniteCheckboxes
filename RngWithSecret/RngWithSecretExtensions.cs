namespace RngWithSecret;

using Microsoft.Extensions.DependencyInjection;

public static class RngWithSecretExtensions
{
    #region Public Methods and Operators

    public static IServiceCollection AddSecretPcg32(this IServiceCollection services)
    {
        services.AddSingleton<SecretPcg32Factory>();
        return services;
    }

    #endregion
}
