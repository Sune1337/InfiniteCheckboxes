namespace APIKeyAuthentication;

using System.Security.Claims;
using System.Text.Encodings.Web;

using APIKeyAuthentication.Options;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

using Two56bitId;

public partial class APIKeyAuthenticationHandler : AuthenticationHandler<APIKeyAuthenticationOptions>
{
    #region Constructors and Destructors

    public APIKeyAuthenticationHandler(IOptionsMonitor<APIKeyAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    #endregion

    #region Methods

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? apiKey = null;

        if (string.IsNullOrEmpty(apiKey) && Request.Query.TryGetValue("access_token", out var accessToken))
        {
            // Get the API key from querystring.
            apiKey = accessToken.ToString();
        }
        else if (Request.Headers.TryGetValue("Authorization", out var authorizationHeaderValue))
        {
            // Get the API key from headers.
            var authorizationHeader = authorizationHeaderValue.ToString();
            if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer "))
            {
                apiKey = authorizationHeader["Bearer ".Length..];
            }
        }
        else if (Request.Headers.TryGetValue(APIKeyConstants.ApiKeyHeaderName, out var apiKeyHeaderValue))
        {
            // Get the API key from headers.
            apiKey = apiKeyHeaderValue.ToString();
        }

        if (apiKey == null || !apiKey.Validate256BitHex())
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Create claims.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, apiKey)
        };

        // Generate claimsIdentity on the name of the class
        var claimsIdentity = new ClaimsIdentity(claims, nameof(APIKeyAuthenticationHandler));

        // Generate AuthenticationTicket from the Identity and current authentication scheme
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);

        // Pass on the ticket to the middleware
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    #endregion
}
