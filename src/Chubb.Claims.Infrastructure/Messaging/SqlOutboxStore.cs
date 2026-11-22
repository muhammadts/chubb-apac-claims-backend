using Chubb.Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chubb.Claims.Infrastructure.Messaging;

public sealed class SqlOutboxStore(ClaimsDbContext db) : IOutboxStore
{
    public async Task<IReadOnlyList<OutboxEnvelope>> LeaseBatchAsync(int batchSize, TimeSpan leaseDuration, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var rows = await db.OutboxMessages.FromSqlInterpolated($"SELECT TOP ({batchSize}) * FROM dbo.OutboxMessages WITH (UPDLOCK, READPAST, ROWLOCK) WHERE PublishedUtc IS NULL AND NextAttemptUtc <= {now} AND (LockedUntilUtc IS NULL OR LockedUntilUtc <= {now}) ORDER BY CreatedUtc")
            .ToListAsync(ct);
        var leaseId = Guid.NewGuid();
        foreach (var row in rows) { row.LockId = leaseId; row.LockedUntilUtc = now + leaseDuration; }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return rows.Select(x => new OutboxEnvelope(x.EventId, leaseId, x.AggregateId, x.EventType,
            x.EventVersion, x.OccurredUtc, x.Payload, x.AttemptCount)).ToArray();
    }

    public async Task MarkPublishedAsync(Guid eventId, Guid leaseId, CancellationToken ct)
    {
        var row = await db.OutboxMessages.SingleOrDefaultAsync(x => x.EventId == eventId && x.LockId == leaseId && x.PublishedUtc == null, ct)
            ?? throw new InvalidOperationException("Outbox lease expired before publish acknowledgement was recorded.");
        row.PublishedUtc = DateTimeOffset.UtcNow; row.LockId = null; row.LockedUntilUtc = null; row.LastError = null;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid eventId, Guid leaseId, string error, TimeSpan retryDelay, CancellationToken ct)
    {
        var row = await db.OutboxMessages.SingleOrDefaultAsync(x => x.EventId == eventId && x.LockId == leaseId && x.PublishedUtc == null, ct)
            ?? throw new InvalidOperationException("Outbox lease expired before failure could be recorded.");
        row.AttemptCount++; row.LastError = error; row.NextAttemptUtc = DateTimeOffset.UtcNow + retryDelay;
        row.LockId = null; row.LockedUntilUtc = null;
        await db.SaveChangesAsync(ct);
    }
}
