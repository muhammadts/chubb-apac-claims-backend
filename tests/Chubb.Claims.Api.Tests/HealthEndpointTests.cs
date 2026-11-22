using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;

namespace Chubb.Claims.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<HealthEndpointTests.TestFactory>
{
    private readonly TestFactory factory;
    public HealthEndpointTests(TestFactory factory) => this.factory = factory;
    [Fact]
    public async Task Health_endpoint_returns_success()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public sealed class TestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Jwt:Issuer", "https://issuer.example.test");
            builder.UseSetting("Jwt:Audience", "claims-api");
            builder.UseSetting("Jwt:Authority", "https://issuer.example.test");
        }
    }
}
