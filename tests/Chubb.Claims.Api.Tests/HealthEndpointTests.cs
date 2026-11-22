using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Chubb.Claims.Api.Tests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_endpoint_returns_success()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
