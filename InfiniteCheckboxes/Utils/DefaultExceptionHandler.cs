namespace InfiniteCheckboxes.Utils;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public class DefaultExceptionHandler : IExceptionHandler
{
    #region Fields

    private readonly ILogger<DefaultExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    #endregion

    #region Constructors and Destructors

    public DefaultExceptionHandler(ILogger<DefaultExceptionHandler> logger, IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    #endregion

    #region Public Methods and Operators

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Log the exception.
        _logger.LogError(exception, "Unhandled error: {ExceptionMessage}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = exception.Message
        };

        return await _problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails,
            }
        );
    }

    #endregion
}
