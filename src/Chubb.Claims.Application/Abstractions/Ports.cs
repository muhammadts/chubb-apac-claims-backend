using Chubb.Claims.Domain.Claims;
using Chubb.Claims.Application.Queries;

namespace Chubb.Claims.Application.Abstractions;

public enum ApplicationRole { Claimant, ClaimsOfficer, Manager }

public interface ICurrentUserContext
{
    string? SubjectId { get; }
    IReadOnlySet<ApplicationRole> Roles { get; }
    IReadOnlySet<string> TeamIds { get; }
}

public interface IClock { DateTimeOffset UtcNow { get; } }
public interface IClaimNumberGenerator { string NextClaimNumber(DateTimeOffset submittedAt); }

/// <summary>One atomic boundary for claim changes, workflow history, activity, and outbox rows.</summary>
public interface IClaimUnitOfWork
{
    Task<IClaimWriteTransaction> BeginAsync(CancellationToken cancellationToken = default);
}

/// <summary>Uncommitted work rolls back on disposal; Commit saves every staged record atomically.</summary>
public interface IClaimWriteTransaction : IAsyncDisposable
{
    Task<Claim?> LoadClaimAsync(Guid claimId, string expectedVersion, CancellationToken cancellationToken = default);
    Task AddClaimAsync(Claim claim, CancellationToken cancellationToken = default);
    Task AddWorkflowRecordAsync(ClaimWorkflowRecord record, CancellationToken cancellationToken = default);
    Task AddActivityAsync(ClaimActivity activity, CancellationToken cancellationToken = default);
    Task AddOutboxMessageAsync(ClaimOutboxMessage message, CancellationToken cancellationToken = default);
    Task<string> CommitAsync(CancellationToken cancellationToken = default);
}

public abstract record ClaimWorkflowRecord(Guid ClaimId, string ActorSubjectId, DateTimeOffset OccurredAt);
public sealed record ClaimAssignmentHistory(Guid ClaimId, string ActorSubjectId, DateTimeOffset OccurredAt,
    string TeamId, string? OfficerSubjectId, string? Reason) : ClaimWorkflowRecord(ClaimId, ActorSubjectId, OccurredAt);
public sealed record ClaimAssessmentHistory(Guid ClaimId, string ActorSubjectId, DateTimeOffset OccurredAt,
    string AssessmentType, string Rationale, string? Recommendation) : ClaimWorkflowRecord(ClaimId, ActorSubjectId, OccurredAt);
public sealed record ClaimInformationRequestHistory(Guid ClaimId, string ActorSubjectId, DateTimeOffset OccurredAt,
    Guid RequestId, string Question) : ClaimWorkflowRecord(ClaimId, ActorSubjectId, OccurredAt);
public sealed record ClaimInformationResponseHistory(Guid ClaimId, string ActorSubjectId, DateTimeOffset OccurredAt,
    Guid RequestId, string? ResponseText) : ClaimWorkflowRecord(ClaimId, ActorSubjectId, OccurredAt);
public sealed record ClaimDecisionHistory(Guid ClaimId, string ActorSubjectId, DateTimeOffset OccurredAt,
    DecisionType Type, string Reason) : ClaimWorkflowRecord(ClaimId, ActorSubjectId, OccurredAt);
public sealed record ClaimSettlementHistory(Guid ClaimId, string ActorSubjectId, DateTimeOffset OccurredAt,
    MoneyEstimate? Amount, string? Reference) : ClaimWorkflowRecord(ClaimId, ActorSubjectId, OccurredAt);
public sealed record ClaimInternalNote(Guid ClaimId, string ActorSubjectId, DateTimeOffset OccurredAt,
    string NoteText) : ClaimWorkflowRecord(ClaimId, ActorSubjectId, OccurredAt);
public sealed record ClaimReserveHistory(Guid ClaimId, string ActorSubjectId, DateTimeOffset OccurredAt,
    MoneyEstimate Estimate, string Reason) : ClaimWorkflowRecord(ClaimId, ActorSubjectId, OccurredAt);

public enum ActivityVisibility { Claimant, Staff, System }
public sealed record ClaimActivity(Guid ClaimId, string ActorSubjectId, DateTimeOffset OccurredAt,
    string ActivityType, ActivityVisibility Visibility, string Summary);
public sealed record ClaimOutboxMessage(Guid EventId, Guid ClaimId, string EventType, int EventVersion,
    DateTimeOffset OccurredAt, IReadOnlyDictionary<string, string> Data);

/// <summary>Continuation tuple for descending timestamp/ID ordering.</summary>
public sealed record PageCursor(DateTimeOffset SortTime, Guid Id);
public sealed record PageRequest(int PageSize, PageCursor? After = null)
{
    public int EffectivePageSize => Math.Clamp(PageSize, 1, 100);
}
public sealed record PageResult<T>(IReadOnlyList<T> Items, PageCursor? Next);

public interface IClaimQueryStore
{
    // Lists use descending timestamp then descending ID keyset order so tied timestamps stay deterministic.
    Task<ClaimantClaimRecord?> FindClaimantClaimAsync(Guid claimId, CancellationToken cancellationToken = default);
    Task<PageResult<ClaimantClaimRecord>> ListClaimantClaimsAsync(string claimantSubjectId, PageRequest page, CancellationToken cancellationToken = default);
    Task<PageResult<ClaimantHistoryRecord>> ListClaimantHistoryAsync(Guid claimId, PageRequest page, CancellationToken cancellationToken = default);
    Task<StaffClaimRecord?> FindStaffClaimAsync(Guid claimId, IReadOnlySet<string> teamIds, CancellationToken cancellationToken = default);
    Task<PageResult<StaffQueueRecord>> ListStaffQueueAsync(WorkQueueFilter filter, IReadOnlySet<string> teamIds, PageRequest page, CancellationToken cancellationToken = default);
    Task<ManagerWorkloadSummary> GetManagerWorkloadAsync(IReadOnlySet<string> teamIds, CancellationToken cancellationToken = default);
    Task<ManagerPerformanceSummary> GetManagerPerformanceAsync(IReadOnlySet<string> teamIds, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default);
    Task<ManagerExposureSummary> GetManagerExposureAsync(IReadOnlySet<string> teamIds, CancellationToken cancellationToken = default);
}

internal static class Authorization
{
    public static string RequireSubject(ICurrentUserContext user)
    {
        if (string.IsNullOrWhiteSpace(user.SubjectId)) throw new AccessDeniedException("An authenticated subject is required.");
        return user.SubjectId.Trim();
    }

    public static void RequireRole(ICurrentUserContext user, ApplicationRole role)
    {
        _ = RequireSubject(user);
        if (!user.Roles.Contains(role)) throw new AccessDeniedException($"The {role} role is required.");
    }

    public static void RequireTeam(ICurrentUserContext user, string teamId)
    {
        if (!user.TeamIds.Contains(teamId)) throw new AccessDeniedException("The claim is outside the caller's team scope.");
    }

    public static string RequireVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) throw new ValidationException("A concurrency token is required.");
        return version;
    }

    public static ClaimAssignment RequireAssignedTeam(Claim claim, ICurrentUserContext user)
    {
        var assignment = claim.Assignment ?? throw new AccessDeniedException("The claim has no team assignment.");
        RequireTeam(user, assignment.TeamId);
        return assignment;
    }
}
