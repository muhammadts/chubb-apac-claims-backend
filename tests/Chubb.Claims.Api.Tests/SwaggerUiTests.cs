using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Chubb.Claims.Api.Tests;

public sealed class SwaggerUiTests : IClassFixture<SwaggerUiTests.TestFactory>
{
    private readonly TestFactory factory;
    public SwaggerUiTests(TestFactory factory) => this.factory = factory;
    [Fact]
    public async Task Development_swagger_ui_uses_existing_openapi_document_and_lists_health()
    {
        using var client = factory.CreateClient();

        using var uiResponse = await client.GetAsync("/swagger");
        var ui = await uiResponse.Content.ReadAsStringAsync();
        using var document = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, uiResponse.StatusCode);
        Assert.Contains("swagger-ui", ui, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);

        using var openApi = JsonDocument.Parse(await document.Content.ReadAsStringAsync());
        Assert.True(openApi.RootElement.GetProperty("paths").TryGetProperty("/health", out _));
    }

    [Fact]
    public async Task Swagger_ui_is_not_available_outside_development()
    {
        await using var productionFactory = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production"));
        using var client = productionFactory.CreateClient();

        using var response = await client.GetAsync("/swagger");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public sealed class TestFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Jwt:Issuer", "https://issuer.example.test");
            builder.UseSetting("Jwt:Audience", "claims-api");
            builder.UseSetting("Jwt:Authority", "https://issuer.example.test");
        }
    }
}
