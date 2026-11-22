using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Chubb.Claims.Application.Abstractions;
using Chubb.Claims.Infrastructure.Persistence;
using Chubb.Claims.Infrastructure.Messaging;

namespace Chubb.Claims.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ClaimsDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("ClaimsDatabase");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Connection string 'ClaimsDatabase' must be configured before database operations are used.");
            options.UseSqlServer(connectionString);
        });
        services.AddScoped<IClaimUnitOfWork, SqlClaimUnitOfWork>();
        services.AddScoped<IClaimQueryStore, SqlClaimQueryStore>();
        var kafka = configuration.GetSection("Claims:Kafka");
        services.Configure<KafkaOutboxOptions>(options =>
        {
            options.BootstrapServers = kafka["BootstrapServers"];
            if (int.TryParse(kafka["BatchSize"], out var batchSize)) options.BatchSize = batchSize;
            if (int.TryParse(kafka["PollSeconds"], out var pollSeconds)) options.PollSeconds = pollSeconds;
            if (int.TryParse(kafka["LeaseSeconds"], out var leaseSeconds)) options.LeaseSeconds = leaseSeconds;
            if (int.TryParse(kafka["MaxRetrySeconds"], out var maxRetrySeconds)) options.MaxRetrySeconds = maxRetrySeconds;
            foreach (var topic in kafka.GetSection("Topics").GetChildren())
                if (!string.IsNullOrWhiteSpace(topic.Value)) options.Topics[topic.Key] = topic.Value;
        });
        if (!string.IsNullOrWhiteSpace(kafka["BootstrapServers"]) && !kafka["BootstrapServers"]!.StartsWith("<", StringComparison.Ordinal))
        {
            services.AddSingleton<IKafkaEventProducer, ConfluentKafkaEventProducer>();
            services.AddScoped<IOutboxStore, SqlOutboxStore>();
            services.AddScoped<OutboxPublisher>();
            services.AddHostedService<OutboxPublisherService>();
        }
        return services;
    }
}
