using Chubb.Claims.Infrastructure;
using Chubb.Claims.Api;
using Chubb.Claims.Application.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.Configure<ClaimsRuntimeOptions>(
    builder.Configuration.GetSection(ClaimsRuntimeOptions.SectionName));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddClaimApplication();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters.RoleClaimType = "role";
        options.Authority = builder.Configuration["Jwt:Authority"];
        options.Audience = builder.Configuration["Jwt:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });
builder.Services.AddAuthorization(options => options.AddPolicy("Claimant", policy =>
{
    policy.RequireAuthenticatedUser();
    policy.RequireRole("Claimant");
    policy.RequireClaim("sub");
}));
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
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

app.Use(async (context, next) =>
{
    try { await next(); }
    catch (Exception exception) when (!context.Response.HasStarted)
    {
        var (status, title) = exception switch
        {
            Chubb.Claims.Application.Abstractions.ValidationException => (StatusCodes.Status400BadRequest, "Request validation failed"),
            System.ComponentModel.DataAnnotations.ValidationException => (StatusCodes.Status400BadRequest, "Request validation failed"),
            AccessDeniedException => (StatusCodes.Status403Forbidden, "Access denied"),
            ResourceNotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Request validation failed"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };
        app.Logger.Log(status >= 500 ? LogLevel.Error : LogLevel.Information,
            exception, "Request failed with status {StatusCode}", status);
        context.Response.Clear();
        context.Response.StatusCode = status;
        await Results.Problem(statusCode: status, title: title, detail: status < 500 ? exception.Message : null)
            .ExecuteAsync(context);
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/openapi/v1.json", "Chubb Claims API v1"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
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
