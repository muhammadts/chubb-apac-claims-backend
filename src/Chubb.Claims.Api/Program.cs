using Chubb.Claims.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.Configure<ClaimsRuntimeOptions>(
    builder.Configuration.GetSection(ClaimsRuntimeOptions.SectionName));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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
