using Chubb.Claims.Application.Abstractions;
using Chubb.Claims.Application.Contracts;
using Chubb.Claims.Domain.Claims;

namespace Chubb.Claims.Application.Queries;

// Claimant contracts intentionally have no internal note, staff assessment, or decision rationale fields.
public sealed record ClaimantClaimCard(Guid ClaimId, string ClaimNumber, string MarketCode, LineOfBusiness LineOfBusiness,
    ClaimStatus Status, DateTimeOffset SubmittedAt, string Version);
public sealed record ClaimantInformationRequestDto(Guid RequestId, string Question, DateTimeOffset RequestedAt,
    bool IsOpen, DateTimeOffset? RespondedAt);
public sealed record ClaimantDecisionDto(DecisionType Type, DateTimeOffset DecidedAt, MoneyEstimateDto? Amount);
public sealed record ClaimantActivityDto(DateTimeOffset OccurredAt, string ActivityType, string Summary);
public sealed record ClaimantClaimDetail(Guid ClaimId, string ClaimNumber, string MarketCode, LineOfBusiness LineOfBusiness,
    ClaimStatus Status, DateTimeOffset ReportedAt, DateTimeOffset SubmittedAt, ClaimantContactDto Contact,
    IncidentDto Incident, ClaimDetailsDto Details, ClaimantDecisionDto? Decision,
    IReadOnlyList<ClaimantInformationRequestDto> InformationRequests, IReadOnlyList<ClaimantActivityDto> Activities,
    string Version);
public sealed record ClaimantHistoryItem(DateTimeOffset OccurredAt, string ActivityType, string Summary);
public sealed record ClaimantClaimRecord(string OwnerSubjectId, ClaimantClaimCard Card, ClaimantClaimDetail? Detail = null);
public sealed record ClaimantHistoryRecord(string OwnerSubjectId, ClaimantHistoryItem Item);

// These distinct staff contracts may contain restricted rationale and internal notes.
public sealed record StaffClaimCard(Guid ClaimId, string ClaimNumber, string MarketCode, LineOfBusiness LineOfBusiness,
    ClaimStatus Status, DateTimeOffset SubmittedAt, string TeamId, string? OfficerSubjectId, MoneyEstimateDto? ReserveEstimate,
    string Version);
public sealed record StaffAssessmentDto(DateTimeOffset RecordedAt, string AssessorSubjectId, string AssessmentType,
    string Rationale, string? Recommendation);
public sealed record StaffNoteDto(DateTimeOffset CreatedAt, string AuthorSubjectId, string NoteText);
public sealed record StaffActivityDto(DateTimeOffset OccurredAt, string ActivityType, string Summary, ActivityVisibility Visibility);
public sealed record StaffClaimDetail(Guid ClaimId, string ClaimNumber, string MarketCode, LineOfBusiness LineOfBusiness,
    ClaimStatus Status, DateTimeOffset ReportedAt, DateTimeOffset SubmittedAt, string ClaimantSubjectId,
    ClaimantContactDto Contact, IncidentDto Incident, ClaimDetailsDto Details, string TeamId, string? OfficerSubjectId,
    MoneyEstimateDto? ReserveEstimate, StaffDecisionDto? Decision, IReadOnlyList<StaffAssessmentDto> Assessments,
    IReadOnlyList<MoneyEstimateDto> ReserveHistory, IReadOnlyList<StaffNoteDto> InternalNotes,
    IReadOnlyList<StaffActivityDto> Activities, string Version);
public sealed record StaffClaimRecord(string TeamId, StaffClaimCard Card, StaffClaimDetail? Detail = null);
public sealed record StaffQueueRecord(string TeamId, StaffClaimCard Card);

public sealed record WorkQueueFilter(string? MarketCode = null, IReadOnlySet<ClaimStatus>? Statuses = null,
    string? TeamId = null, string? OfficerSubjectId = null, DateTimeOffset? SubmittedFromUtc = null,
    DateTimeOffset? SubmittedToUtc = null, string? SearchText = null);
public sealed record GetClaimantClaim(Guid ClaimId);
public sealed record ListClaimantClaims(PageRequest Page);
public sealed record GetClaimantHistory(Guid ClaimId, PageRequest Page);
public sealed record GetStaffClaim(Guid ClaimId);
public sealed record ListStaffWorkQueue(WorkQueueFilter Filter, PageRequest Page);
public sealed record GetManagerWorkload;
public sealed record GetManagerPerformance(DateTimeOffset FromUtc, DateTimeOffset ToUtc);
public sealed record GetManagerExposure;

public sealed record StatusCount(ClaimStatus Status, int Count);
public sealed record WorkloadGroup(string MarketCode, string? TeamId, string? OfficerSubjectId,
    IReadOnlyList<StatusCount> StatusCounts, int UnassignedCount, IReadOnlyDictionary<string, int> AgeingBands);
public sealed record ManagerWorkloadSummary(DateTimeOffset AsOfUtc, IReadOnlyList<WorkloadGroup> Groups,
    string AgeingDefinition);
public sealed record ManagerPerformanceSummary(DateTimeOffset FromUtc, DateTimeOffset ToUtc, int DecisionCount,
    double? AverageDecisionElapsedDays, double? MedianDecisionElapsedDays, int DecisionsMissingDuration,
    int SettlementCount, double? AverageSettlementElapsedDays, int SettlementsMissingDuration);
public sealed record ExposureGroup(string MarketCode, string Currency, decimal Amount, int ClaimCount);
public sealed record ManagerExposureSummary(DateTimeOffset AsOfUtc, IReadOnlyList<ExposureGroup> Groups,
    int ClaimsMissingReserve, IReadOnlyList<ClaimStatus> IncludedStatuses, string Assumption);

public sealed class GetClaimantClaimHandler(IClaimQueryStore store, ICurrentUserContext user)
    : IQueryHandler<GetClaimantClaim, ClaimantClaimDetail>
{
    public async Task<ClaimantClaimDetail> HandleAsync(GetClaimantClaim query, CancellationToken cancellationToken = default)
    {
        Authorization.RequireRole(user, ApplicationRole.Claimant);
        var subject = Authorization.RequireSubject(user);
        var record = await store.FindClaimantClaimAsync(query.ClaimId, cancellationToken)
            ?? throw new ResourceNotFoundException("Claim was not found.");
        if (!string.Equals(record.OwnerSubjectId, subject, StringComparison.Ordinal))
            throw new ResourceNotFoundException("Claim was not found.");
        return record.Detail ?? throw new ResourceNotFoundException("Claim was not found.");
    }
}

public sealed class ListClaimantClaimsHandler(IClaimQueryStore store, ICurrentUserContext user)
    : IQueryHandler<ListClaimantClaims, PageResult<ClaimantClaimCard>>
{
    public async Task<PageResult<ClaimantClaimCard>> HandleAsync(ListClaimantClaims query, CancellationToken cancellationToken = default)
    {
        Authorization.RequireRole(user, ApplicationRole.Claimant);
        var subject = Authorization.RequireSubject(user);
        var page = await store.ListClaimantClaimsAsync(subject, query.Page with { PageSize = query.Page.EffectivePageSize }, cancellationToken);
        if (page.Items.Any(item => !string.Equals(item.OwnerSubjectId, subject, StringComparison.Ordinal)))
            throw new AccessDeniedException("Claimant query returned a record outside the caller's ownership scope.");
        return new PageResult<ClaimantClaimCard>(page.Items.Select(item => item.Card).ToArray(), page.Next);
    }
}

public sealed class GetClaimantHistoryHandler(IClaimQueryStore store, ICurrentUserContext user)
    : IQueryHandler<GetClaimantHistory, PageResult<ClaimantHistoryItem>>
{
    public async Task<PageResult<ClaimantHistoryItem>> HandleAsync(GetClaimantHistory query, CancellationToken cancellationToken = default)
    {
        Authorization.RequireRole(user, ApplicationRole.Claimant);
        var subject = Authorization.RequireSubject(user);
        var claim = await store.FindClaimantClaimAsync(query.ClaimId, cancellationToken)
            ?? throw new ResourceNotFoundException("Claim was not found.");
        if (!string.Equals(claim.OwnerSubjectId, subject, StringComparison.Ordinal))
            throw new ResourceNotFoundException("Claim was not found.");
        var page = await store.ListClaimantHistoryAsync(query.ClaimId,
            query.Page with { PageSize = query.Page.EffectivePageSize }, cancellationToken);
        if (page.Items.Any(item => !string.Equals(item.OwnerSubjectId, subject, StringComparison.Ordinal)))
            throw new AccessDeniedException("Claimant history query returned a record outside the caller's ownership scope.");
        return new PageResult<ClaimantHistoryItem>(page.Items.Select(item => item.Item).ToArray(), page.Next);
    }
}

public sealed class GetStaffClaimHandler(IClaimQueryStore store, ICurrentUserContext user)
    : IQueryHandler<GetStaffClaim, StaffClaimDetail>
{
    public async Task<StaffClaimDetail> HandleAsync(GetStaffClaim query, CancellationToken cancellationToken = default)
    {
        RequireStaff(user);
        var record = await store.FindStaffClaimAsync(query.ClaimId, user.TeamIds, cancellationToken)
            ?? throw new ResourceNotFoundException("Claim was not found.");
        Authorization.RequireTeam(user, record.TeamId);
        return record.Detail ?? throw new ResourceNotFoundException("Claim was not found.");
    }

    internal static void RequireStaff(ICurrentUserContext user)
    {
        if (!user.Roles.Contains(ApplicationRole.ClaimsOfficer) && !user.Roles.Contains(ApplicationRole.Manager))
            throw new AccessDeniedException("ClaimsOfficer or Manager role is required.");
        _ = Authorization.RequireSubject(user);
    }
}

public sealed class ListStaffWorkQueueHandler(IClaimQueryStore store, ICurrentUserContext user)
    : IQueryHandler<ListStaffWorkQueue, PageResult<StaffClaimCard>>
{
    public async Task<PageResult<StaffClaimCard>> HandleAsync(ListStaffWorkQueue query, CancellationToken cancellationToken = default)
    {
        GetStaffClaimHandler.RequireStaff(user);
        if (query.Filter.TeamId is not null) Authorization.RequireTeam(user, query.Filter.TeamId);
        var page = await store.ListStaffQueueAsync(query.Filter, user.TeamIds,
            query.Page with { PageSize = query.Page.EffectivePageSize }, cancellationToken);
        if (page.Items.Any(item => !user.TeamIds.Contains(item.TeamId)))
            throw new AccessDeniedException("Staff queue query returned a record outside the caller's team scope.");
        return new PageResult<StaffClaimCard>(page.Items.Select(item => item.Card).ToArray(), page.Next);
    }
}

public sealed class GetManagerWorkloadHandler(IClaimQueryStore store, ICurrentUserContext user)
    : IQueryHandler<GetManagerWorkload, ManagerWorkloadSummary>
{
    public Task<ManagerWorkloadSummary> HandleAsync(GetManagerWorkload query, CancellationToken cancellationToken = default)
    {
        RequireManager(user);
        return store.GetManagerWorkloadAsync(user.TeamIds, cancellationToken);
    }
    internal static void RequireManager(ICurrentUserContext user)
    {
        Authorization.RequireRole(user, ApplicationRole.Manager);
        if (user.TeamIds.Count == 0) throw new AccessDeniedException("The manager has no team scope.");
    }
}

public sealed class GetManagerPerformanceHandler(IClaimQueryStore store, ICurrentUserContext user)
    : IQueryHandler<GetManagerPerformance, ManagerPerformanceSummary>
{
    public Task<ManagerPerformanceSummary> HandleAsync(GetManagerPerformance query, CancellationToken cancellationToken = default)
    {
        GetManagerWorkloadHandler.RequireManager(user);
        if (query.FromUtc >= query.ToUtc) throw new ValidationException("The reporting start must precede the reporting end.");
        return store.GetManagerPerformanceAsync(user.TeamIds, query.FromUtc, query.ToUtc, cancellationToken);
    }
}

public sealed class GetManagerExposureHandler(IClaimQueryStore store, ICurrentUserContext user)
    : IQueryHandler<GetManagerExposure, ManagerExposureSummary>
{
    public Task<ManagerExposureSummary> HandleAsync(GetManagerExposure query, CancellationToken cancellationToken = default)
    {
        GetManagerWorkloadHandler.RequireManager(user);
        return store.GetManagerExposureAsync(user.TeamIds, cancellationToken);
    }
}
