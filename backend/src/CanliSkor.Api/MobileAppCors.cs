namespace CanliSkor.Api;

/// <summary>
/// Lets the Android app call the API and the hub. The website needs none of this (it is served from this same
/// origin); the app's pages are loaded from the phone itself, so to the browser inside it every call here is
/// cross-origin. Only the app's own origins are allowed, never "any".
/// </summary>
public static class MobileAppCors
{
    /// <summary>
    /// The origins a Capacitor app's pages have: <c>https://localhost</c> on Android (the default),
    /// <c>http://localhost</c> on Android with the http scheme, <c>capacitor://localhost</c> on iOS.
    /// </summary>
    public static readonly string[] Origins = ["https://localhost", "http://localhost", "capacitor://localhost"];

    public static IServiceCollection AddMobileAppCors(this IServiceCollection services) =>
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(Origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // SignalR's browser client sends its requests "with credentials", which a browser only accepts
            // from a server that names the origin and allows them.
            .AllowCredentials()));
}
