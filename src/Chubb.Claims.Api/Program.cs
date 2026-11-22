using Chubb.Claims.Infrastructure;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.Configure<ClaimsRuntimeOptions>(
    builder.Configuration.GetSection(ClaimsRuntimeOptions.SectionName));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Paths.Add("/health", new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [HttpMethod.Get] = new()
                {
                    OperationId = "Health",
                    Summary = "Returns the API health status.",
                    Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse { Description = "The API is healthy." }
                    }
                }
            }
        });

        return Task.CompletedTask;
    }));
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Chubb Claims API v1"));
}

app.UseHttpsRedirection();
app.MapHealthChecks("/health").WithName("Health");

app.Run();

public partial class Program { }

public sealed class ClaimsRuntimeOptions
{
    public const string SectionName = "Claims";

    public KafkaRuntimeOptions Kafka { get; set; } = new();
}

public sealed class KafkaRuntimeOptions
{
    public string? BootstrapServers { get; set; }
}
