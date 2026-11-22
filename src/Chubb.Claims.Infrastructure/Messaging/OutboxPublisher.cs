using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chubb.Claims.Infrastructure.Messaging;

public sealed record OutboxEnvelope(Guid EventId, Guid LeaseId, Guid AggregateId, string EventType,
    short EventVersion, DateTimeOffset OccurredUtc, string Payload, int AttemptCount);

public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxEnvelope>> LeaseBatchAsync(int batchSize, TimeSpan leaseDuration, CancellationToken ct);
    Task MarkPublishedAsync(Guid eventId, Guid leaseId, CancellationToken ct);
    Task MarkFailedAsync(Guid eventId, Guid leaseId, string error, TimeSpan retryDelay, CancellationToken ct);
}

public interface IKafkaEventProducer : IDisposable
{
    Task ProduceAsync(string topic, string key, string value, CancellationToken ct);
}

public sealed class ConfluentKafkaEventProducer(IOptions<KafkaOutboxOptions> options) : IKafkaEventProducer
{
    private readonly IProducer<string, string> _producer = new ProducerBuilder<string, string>(
        new ProducerConfig { BootstrapServers = options.Value.BootstrapServers }).Build();
    public async Task ProduceAsync(string topic, string key, string value, CancellationToken ct) =>
        _ = await _producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = value }, ct);
    public void Dispose() => _producer.Dispose();
}

public sealed class KafkaOutboxOptions
{
    public string? BootstrapServers { get; set; }
    public int BatchSize { get; set; } = 20;
    public int PollSeconds { get; set; } = 2;
    public int LeaseSeconds { get; set; } = 60;
    public int MaxRetrySeconds { get; set; } = 300;
    public Dictionary<string, string> Topics { get; set; } = new(StringComparer.Ordinal)
    {
        ["ClaimSubmitted"] = "claims.submitted.v1", ["ClaimAssigned"] = "claims.assigned.v1",
        ["ClaimInformationRequested"] = "claims.information-requested.v1", ["ClaimStatusChanged"] = "claims.status-changed.v1",
        ["ClaimDecisionRecorded"] = "claims.decision-recorded.v1"
    };
}

public sealed class OutboxPublisher(IOutboxStore store, IKafkaEventProducer producer,
    IOptions<KafkaOutboxOptions> options, ILogger<OutboxPublisher> logger)
{
    public async Task<int> PublishBatchAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var batch = await store.LeaseBatchAsync(Math.Clamp(settings.BatchSize, 1, 100),
            TimeSpan.FromSeconds(Math.Clamp(settings.LeaseSeconds, 5, 600)), ct);
        var published = 0;
        foreach (var message in batch)
        {
            try
            {
                if (!settings.Topics.TryGetValue(message.EventType, out var topic) || string.IsNullOrWhiteSpace(topic))
                    throw new InvalidOperationException($"No Kafka topic is configured for event type {message.EventType}.");
                using var document = JsonDocument.Parse(message.Payload);
                var value = JsonSerializer.Serialize(new { message.EventId, message.EventType, message.EventVersion,
                    message.OccurredUtc, message.AggregateId, data = document.RootElement });
                await producer.ProduceAsync(topic, message.AggregateId.ToString("D"), value, ct);
                await store.MarkPublishedAsync(message.EventId, message.LeaseId, ct);
                published++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                var attempt = message.AttemptCount + 1;
                var seconds = Math.Min(Math.Max(1, settings.MaxRetrySeconds), 1 << Math.Min(attempt, 20));
                await store.MarkFailedAsync(message.EventId, message.LeaseId,
                    exception.Message[..Math.Min(2000, exception.Message.Length)], TimeSpan.FromSeconds(seconds), ct);
                logger.LogWarning(exception, "Kafka publish failed for outbox event {EventId}; attempt {Attempt}", message.EventId, attempt);
            }
        }
        return published;
    }
}

public sealed class OutboxPublisherService(IServiceScopeFactory scopes, IOptions<KafkaOutboxOptions> options,
    ILogger<OutboxPublisherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Clamp(options.Value.PollSeconds, 1, 60));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<OutboxPublisher>().PublishBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Outbox publisher iteration failed."); }
            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
