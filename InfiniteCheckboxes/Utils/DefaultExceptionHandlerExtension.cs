namespace InfiniteCheckboxes.Utils;

using System.Net;

public static class DefaultExceptionHandlerExtension
{
    #region Constants

    private const int InternalServerErrorStatusCode = (int)HttpStatusCode.InternalServerError;

    #endregion

    #region Public Methods and Operators

    public static IServiceCollection AddDefaultExceptionHandler(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            {
                options.CustomizeProblemDetails = context =>
                {
                    // Add RequestId to problem-details.
                    context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
                };
            }
        );
        services.AddExceptionHandler<DefaultExceptionHandler>();

        return services;
    }

    #endregion
}
