namespace InfiniteCheckboxes.Utils;

using Microsoft.AspNetCore.Routing.Patterns;

public static class SpaFallback
{
    #region Public Methods and Operators

    public static WebApplication MapSpaFallback(this WebApplication webApplication, StaticFileOptions? options = null)
    {
        var host = webApplication.Services.GetRequiredService<IWebHostEnvironment>();
        var webRootPath = host.WebRootPath;

        webApplication.Map(RoutePatternFactory.Parse("/{directory}/{**path}"), CreateRequestDelegate(webApplication, webRootPath, options));
        return webApplication;
    }

    #endregion

    #region Methods

    private static RequestDelegate CreateRequestDelegate(IEndpointRouteBuilder endpoints,
        string webRootPath,
        StaticFileOptions? options = null)
    {
        var app = endpoints.CreateApplicationBuilder();
        app.Use(next => context =>
        {
            string? path = null;
            if (context.Request.RouteValues.TryGetValue("directory", out var directory))
            {
                path = $"/{directory}/index.html";
                if (!File.Exists(Path.Join(webRootPath, $"/{directory}/index.html")))
                {
                    path = null;
                }
            }

            context.Request.Path = path ?? "/index.html";

            // Set endpoint to null so the static files middleware will handle the request.
            context.SetEndpoint(null);

            return next(context);
        });

        if (options == null)
        {
            app.UseStaticFiles();
        }
        else
        {
            app.UseStaticFiles(options);
        }

        return app.Build();
    }

    #endregion
}
