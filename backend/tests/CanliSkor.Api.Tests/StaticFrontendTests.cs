using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace CanliSkor.Api.Tests;

/// <summary>The built React app is served from wwwroot next to the API, with cache headers that fit Vite's output.</summary>
public class StaticFrontendTests(StaticFrontendTests.FrontendFactory factory) : IClassFixture<StaticFrontendTests.FrontendFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Root_serves_index_html_that_must_be_revalidated()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<div id=\"root\">", await response.Content.ReadAsStringAsync());
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task Hashed_assets_are_cached_forever()
    {
        var response = await _client.GetAsync("/assets/index-abc123.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=31536000, immutable", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Api_still_works_next_to_the_frontend()
    {
        var response = await _client.GetAsync("/api/leagues");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    /// <summary>Points wwwroot at a temp folder holding a minimal fake build.</summary>
    public sealed class FrontendFactory : MatchEndpointsTests.Factory
    {
        private readonly string _webRoot = Directory.CreateTempSubdirectory("canliskor-wwwroot-").FullName;

        public FrontendFactory()
        {
            Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
            File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><div id=\"root\"></div>");
            File.WriteAllText(Path.Combine(_webRoot, "assets", "index-abc123.js"), "console.log('app')");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseWebRoot(_webRoot);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            // WebApplicationFactory runs Dispose(bool) more than once during teardown.
            if (Directory.Exists(_webRoot))
            {
                Directory.Delete(_webRoot, recursive: true);
            }
        }
    }
}
