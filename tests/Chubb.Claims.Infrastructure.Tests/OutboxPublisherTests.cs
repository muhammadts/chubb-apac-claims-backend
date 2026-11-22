using Chubb.Claims.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Chubb.Claims.Infrastructure.Tests;

public sealed class OutboxPublisherTests
{
    [Fact]
    public async Task SuccessfulBrokerAcknowledgementMarksMessagePublished()
    {
        var store = new FakeStore(); var producer = new FakeProducer(); var publisher = Create(store, producer);
        Assert.Equal(1, await publisher.PublishBatchAsync(CancellationToken.None));
        Assert.Equal(1, producer.Count); Assert.Equal(1, store.PublishedCount); Assert.Equal(0, store.FailureCount);
    }

    [Fact]
    public async Task FailedProducePersistsFailureAndRetriesWithSameEventId()
    {
        var store = new FakeStore(); var producer = new FakeProducer { FailNext = true }; var publisher = Create(store, producer);
        Assert.Equal(0, await publisher.PublishBatchAsync(CancellationToken.None));
        Assert.Equal(1, store.FailureCount); Assert.Equal(1, store.AttemptCount); Assert.NotNull(store.LastError);
        Assert.Equal(1, await publisher.PublishBatchAsync(CancellationToken.None));
        Assert.Equal(2, producer.Count); Assert.Equal(producer.EventIds[0], producer.EventIds[1]);
        Assert.Equal(1, store.PublishedCount);
    }

    [Fact]
    public async Task AcknowledgedThenLeaseRecoveryCanRedeliverAndKeepsEventIdForDeduplication()
    {
        var store = new FakeStore { SimulateAckPersistenceLoss = true }; var producer = new FakeProducer(); var publisher = Create(store, producer);
        Assert.Equal(0, await publisher.PublishBatchAsync(CancellationToken.None));
        store.RecoverExpiredLease();
        Assert.Equal(1, await publisher.PublishBatchAsync(CancellationToken.None));
        Assert.Equal(2, producer.Count); Assert.Equal(producer.EventIds[0], producer.EventIds[1]);
    }

    private static OutboxPublisher Create(FakeStore store, FakeProducer producer) =>
        new(store, producer, Options.Create(new KafkaOutboxOptions()), NullLogger<OutboxPublisher>.Instance);

    private sealed class FakeProducer : IKafkaEventProducer
    {
        public int Count { get; private set; }
        public bool FailNext { get; set; }
        public List<Guid> EventIds { get; } = [];
        public Task ProduceAsync(string topic, string key, string value, CancellationToken ct)
        {
            Count++; using var json = System.Text.Json.JsonDocument.Parse(value);
            EventIds.Add(json.RootElement.GetProperty("EventId").GetGuid());
            if (FailNext) { FailNext = false; throw new InvalidOperationException("broker unavailable"); }
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }

    private sealed class FakeStore : IOutboxStore
    {
        private readonly Guid _eventId = Guid.NewGuid(); private Guid _lease = Guid.NewGuid(); private bool _available = true;
        public int PublishedCount { get; private set; }
        public int FailureCount { get; private set; }
        public int AttemptCount { get; private set; }
        public string? LastError { get; private set; }
        public bool SimulateAckPersistenceLoss { get; set; }
        public Task<IReadOnlyList<OutboxEnvelope>> LeaseBatchAsync(int batchSize, TimeSpan leaseDuration, CancellationToken ct)
        {
            if (!_available) return Task.FromResult<IReadOnlyList<OutboxEnvelope>>([]);
            _available = false; _lease = Guid.NewGuid();
            return Task.FromResult<IReadOnlyList<OutboxEnvelope>>([new(_eventId, _lease, Guid.NewGuid(), "ClaimSubmitted", 1, DateTimeOffset.UtcNow, "{}", AttemptCount)]);
        }
        public Task MarkPublishedAsync(Guid eventId, Guid leaseId, CancellationToken ct)
        {
            Assert.Equal(_eventId, eventId); Assert.Equal(_lease, leaseId);
            if (SimulateAckPersistenceLoss) { SimulateAckPersistenceLoss = false; throw new InvalidOperationException("database unavailable after broker ack"); }
            PublishedCount++; return Task.CompletedTask;
        }
        public Task MarkFailedAsync(Guid eventId, Guid leaseId, string error, TimeSpan retryDelay, CancellationToken ct)
        { Assert.Equal(_eventId, eventId); Assert.Equal(_lease, leaseId); AttemptCount++; FailureCount++; LastError = error; _available = true; return Task.CompletedTask; }
        public void RecoverExpiredLease() => _available = true;
    }
}
