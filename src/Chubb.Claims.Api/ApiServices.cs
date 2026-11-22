using System.Security.Claims;
using Chubb.Claims.Application.Abstractions;
using Chubb.Claims.Application.Commands;
using Chubb.Claims.Application.Contracts;
using Chubb.Claims.Application.Queries;

namespace Chubb.Claims.Api;

public sealed class HttpCurrentUserContext(IHttpContextAccessor accessor) : ICurrentUserContext
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;
    public string? SubjectId => Principal?.FindFirst("sub")?.Value
        ?? Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public IReadOnlySet<ApplicationRole> Roles => (Principal?.FindAll(ClaimTypes.Role)
            .Concat(Principal.FindAll("role")).Select(claim => claim.Value).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(role => Enum.TryParse<ApplicationRole>(role, true, out var parsed) ? parsed : (ApplicationRole?)null)
            .Where(role => role.HasValue).Select(role => role!.Value).ToHashSet()) ?? new HashSet<ApplicationRole>();
    public IReadOnlySet<string> TeamIds => (Principal?.FindAll("team").Concat(Principal.FindAll("teams"))
            .Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal)) ?? new HashSet<string>(StringComparer.Ordinal);
    public IReadOnlySet<string> MarketCodes => (Principal?.FindAll("market").Concat(Principal.FindAll("markets"))
            .Select(claim => claim.Value).ToHashSet(StringComparer.OrdinalIgnoreCase)) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

/// <summary>Demo claim number format; replace with the approved market format before production.</summary>
public sealed class DemoClaimNumberGenerator : IClaimNumberGenerator
{
    public string NextClaimNumber(DateTimeOffset submittedAt) => $"CLM-{submittedAt:yyyyMMdd}-{Guid.NewGuid():N}"[..21].ToUpperInvariant();
}

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddClaimApplication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IClaimNumberGenerator, DemoClaimNumberGenerator>();
        services.AddScoped<ICommandHandler<SubmitClaim, ClaimCommandResult>, SubmitClaimHandler>();
        services.AddScoped<ICommandHandler<SubmitInformationResponse, ClaimCommandResult>, SubmitInformationResponseHandler>();
        services.AddScoped<ICommandHandler<AssignClaim, ClaimCommandResult>, AssignClaimHandler>();
        services.AddScoped<ICommandHandler<StartReview, ClaimCommandResult>, StartReviewHandler>();
        services.AddScoped<ICommandHandler<RequestInformation, ClaimCommandResult>, RequestInformationHandler>();
        services.AddScoped<ICommandHandler<ResumeReview, ClaimCommandResult>, ResumeReviewHandler>();
        services.AddScoped<ICommandHandler<RecordAssessment, ClaimCommandResult>, RecordAssessmentHandler>();
        services.AddScoped<ICommandHandler<RecordFinalDecision, ClaimCommandResult>, RecordFinalDecisionHandler>();
        services.AddScoped<ICommandHandler<CompleteSettlementWorkflow, ClaimCommandResult>, CompleteSettlementWorkflowHandler>();
        services.AddScoped<ICommandHandler<AddInternalNote, ClaimCommandResult>, AddInternalNoteHandler>();
        services.AddScoped<IQueryHandler<ListClaimantClaims, PageResult<ClaimantClaimCard>>, ListClaimantClaimsHandler>();
        services.AddScoped<IQueryHandler<GetClaimantClaim, ClaimantClaimDetail>, GetClaimantClaimHandler>();
        services.AddScoped<IQueryHandler<GetClaimantHistory, PageResult<ClaimantHistoryItem>>, GetClaimantHistoryHandler>();
        services.AddScoped<IQueryHandler<ListStaffWorkQueue, PageResult<StaffClaimCard>>, ListStaffWorkQueueHandler>();
        services.AddScoped<IQueryHandler<GetStaffClaim, StaffClaimDetail>, GetStaffClaimHandler>();
        services.AddScoped<IQueryHandler<GetManagerWorkload, ManagerWorkloadSummary>, GetManagerWorkloadHandler>();
        services.AddScoped<IQueryHandler<GetManagerPerformance, ManagerPerformanceSummary>, GetManagerPerformanceHandler>();
        services.AddScoped<IQueryHandler<GetManagerExposure, ManagerExposureSummary>, GetManagerExposureHandler>();
        return services;
    }
}
