namespace InfiniteCheckboxes.Utils;

using System.Security.Claims;

using Serilog;

public static class SerilogRequestLoggingExtensions
{
    #region Public Methods and Operators

    public static IApplicationBuilder UseEnrichedSerilogRequestLogging(this IApplicationBuilder app)
    {
        return app
            .UseSerilogRequestLogging(options =>
            {
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    var nameIdentifier = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (string.IsNullOrEmpty(nameIdentifier) == false)
                    {
                        diagnosticContext.Set("NameIdentifier", nameIdentifier);
                    }

                    var userAgent = httpContext.Request.Headers.UserAgent.ToString();
                    if (string.IsNullOrEmpty(userAgent) == false)
                    {
                        diagnosticContext.Set("UserAgent", userAgent);
                    }

                    var remoteIpAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                    if (string.IsNullOrEmpty(remoteIpAddress) == false)
                    {
                        diagnosticContext.Set("RemoteIpAddress", remoteIpAddress);
                    }

                    var xForwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
                    if (string.IsNullOrEmpty(xForwardedFor) == false)
                    {
                        diagnosticContext.Set("X-Forwarded-For", xForwardedFor);
                    }
                };
            });
    }

    #endregion
}
