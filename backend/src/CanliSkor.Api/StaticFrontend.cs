using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace CanliSkor.Api;

/// <summary>
/// Serves the built React app (frontend/dist, copied into wwwroot at publish time) from the same origin
/// as the API and hub, so production needs no CORS either. In development wwwroot is empty and Vite serves the UI.
/// </summary>
public static class StaticFrontend
{
    public static WebApplication UseStaticFrontend(this WebApplication app)
    {
        // Development: no build copied in, Vite serves the UI (and the static file middleware would only warn).
        if (!Directory.Exists(app.Environment.WebRootPath))
        {
            return app;
        }

        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = SetCacheHeaders });
        return app;
    }

    private static void SetCacheHeaders(StaticFileResponseContext context)
    {
        var headers = context.Context.Response.Headers;

        // Vite puts a content hash in every file name under /assets: a changed file gets a new URL,
        // so these can be cached forever.
        if (context.Context.Request.Path.StartsWithSegments("/assets"))
        {
            headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
            return;
        }

        // index.html (and other unhashed files) must be revalidated, or users keep loading an old build
        // that points at assets which no longer exist.
        headers[HeaderNames.CacheControl] = "no-cache";
    }
}
