using Chubb.Claims.Application.Abstractions;
using Chubb.Claims.Application.Contracts;
using Chubb.Claims.Domain.Claims;

namespace Chubb.Claims.Application.Commands;

public sealed record SubmitClaim(Guid ClaimId, string MarketCode, LineOfBusiness LineOfBusiness,
    ClaimantContactInput Contact, IncidentInput Incident, ClaimDetailsInput Details, DateTimeOffset ReportedAt);
public sealed record AssignClaim(Guid ClaimId, string ExpectedVersion, string TeamId, string? OfficerSubjectId, string? Reason = null);
public sealed record StartReview(Guid ClaimId, string ExpectedVersion);
public sealed record RequestInformation(Guid ClaimId, string ExpectedVersion, string Question);
public sealed record SubmitInformationResponse(Guid ClaimId, string ExpectedVersion, Guid RequestId, string? ResponseText);
public sealed record ResumeReview(Guid ClaimId, string ExpectedVersion);
public sealed record RecordAssessment(Guid ClaimId, string ExpectedVersion, string AssessmentType,
    string Rationale, string? Recommendation, MoneyEstimateInput? ReserveEstimate, string? ReserveReason);
public sealed record RecordFinalDecision(Guid ClaimId, string ExpectedVersion, DecisionType Type, string Reason, MoneyEstimateInput? Amount);
public sealed record CompleteSettlementWorkflow(Guid ClaimId, string ExpectedVersion, MoneyEstimateInput? Amount, string? Reference);
public sealed record AddInternalNote(Guid ClaimId, string ExpectedVersion, string NoteText);
public sealed record ClaimCommandResult(Guid ClaimId, string ClaimNumber, ClaimStatus Status, string Version);

public abstract class ClaimCommandHandlerBase(IClaimUnitOfWork unitOfWork, ICurrentUserContext user, IClock clock)
{
    protected readonly IClaimUnitOfWork UnitOfWork = unitOfWork;
    protected readonly ICurrentUserContext User = user;
    protected readonly IClock Clock = clock;

    protected async Task<(IClaimWriteTransaction Transaction, Claim Claim, string Actor)> LoadStaffClaimAsync(
        Guid claimId, string version, CancellationToken cancellationToken)
    {
        _ = Authorization.RequireVersion(version);
        Authorization.RequireRole(User, ApplicationRole.ClaimsOfficer);
        var actor = Authorization.RequireSubject(User);
        var tx = await UnitOfWork.BeginAsync(cancellationToken);
        try
        {
            var claim = await tx.LoadClaimAsync(claimId, version, cancellationToken)
                ?? throw new ResourceNotFoundException("Claim was not found.");
            _ = Authorization.RequireAssignedTeam(claim, User);
            return (tx, claim, actor);
        }
        catch
        {
            await tx.DisposeAsync();
            throw;
        }
    }

    protected async Task<ClaimCommandResult> FinishAsync(IClaimWriteTransaction tx, Claim claim,
        string actor, DateTimeOffset at, string activityType, ActivityVisibility visibility, string summary,
        string eventType, IReadOnlyDictionary<string, string> eventData, CancellationToken cancellationToken)
    {
        await tx.AddActivityAsync(new ClaimActivity(claim.Id, actor, at, activityType, visibility, summary), cancellationToken);
        await tx.AddOutboxMessageAsync(new ClaimOutboxMessage(Guid.NewGuid(), claim.Id, eventType, 1, at, eventData), cancellationToken);
        var version = await tx.CommitAsync(cancellationToken);
        return new ClaimCommandResult(claim.Id, claim.ClaimNumber, claim.Status, version);
    }

    protected static IReadOnlyDictionary<string, string> Data(params (string Key, string Value)[] values) =>
        values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    protected static void RequireText(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{field} is required.");
    }
}

public sealed class SubmitClaimHandler(IClaimUnitOfWork unitOfWork, ICurrentUserContext user,
    IClaimNumberGenerator claimNumbers, IClock clock) : ICommandHandler<SubmitClaim, ClaimCommandResult>
{
    public async Task<ClaimCommandResult> HandleAsync(SubmitClaim command, CancellationToken cancellationToken = default)
    {
        Authorization.RequireRole(user, ApplicationRole.Claimant);
        var actor = Authorization.RequireSubject(user);
        var now = clock.UtcNow;
        var claim = new Claim(command.ClaimId, claimNumbers.NextClaimNumber(now), actor, command.Contact.ToDomain(),
            command.MarketCode, command.LineOfBusiness, command.Incident.ToDomain(), command.Details.ToDomain(), command.ReportedAt, now);
        await using var tx = await unitOfWork.BeginAsync(cancellationToken);
        await tx.AddClaimAsync(claim, cancellationToken);
        await tx.AddActivityAsync(new ClaimActivity(claim.Id, actor, now, "ClaimSubmitted", ActivityVisibility.Claimant,
            "Claim submitted."), cancellationToken);
        await tx.AddOutboxMessageAsync(new ClaimOutboxMessage(Guid.NewGuid(), claim.Id, "ClaimSubmitted", 1, now,
            new Dictionary<string, string> { ["claimId"] = claim.Id.ToString("D"), ["claimNumber"] = claim.ClaimNumber,
                ["marketCode"] = claim.MarketCode, ["lineOfBusiness"] = claim.LineOfBusiness.ToString() }), cancellationToken);
        var version = await tx.CommitAsync(cancellationToken);
        return new ClaimCommandResult(claim.Id, claim.ClaimNumber, claim.Status, version);
    }
}

public sealed class AssignClaimHandler(IClaimUnitOfWork unitOfWork, ICurrentUserContext user, IClock clock)
    : ICommandHandler<AssignClaim, ClaimCommandResult>
{
    public async Task<ClaimCommandResult> HandleAsync(AssignClaim command, CancellationToken cancellationToken = default)
    {
        _ = Authorization.RequireVersion(command.ExpectedVersion);
        var actor = Authorization.RequireSubject(user);
        if (!user.Roles.Contains(ApplicationRole.ClaimsOfficer) && !user.Roles.Contains(ApplicationRole.Manager))
            throw new AccessDeniedException("ClaimsOfficer or Manager role is required.");
        Authorization.RequireTeam(user, command.TeamId);
        await using var tx = await unitOfWork.BeginAsync(cancellationToken);
        var claim = await tx.LoadClaimAsync(command.ClaimId, command.ExpectedVersion, cancellationToken)
            ?? throw new ResourceNotFoundException("Claim was not found.");
        if (claim.Assignment is not null) Authorization.RequireTeam(user, claim.Assignment.TeamId);
        if (!string.IsNullOrWhiteSpace(command.OfficerSubjectId) && !user.Roles.Contains(ApplicationRole.Manager)
            && command.OfficerSubjectId != actor)
            throw new AccessDeniedException("An officer can assign only themselves within their team.");
        var now = clock.UtcNow;
        var assignment = new ClaimAssignment(command.TeamId.Trim(), command.OfficerSubjectId?.Trim(), actor, now, command.Reason?.Trim());
        claim.Assign(assignment);
        await tx.AddWorkflowRecordAsync(new ClaimAssignmentHistory(claim.Id, actor, now, assignment.TeamId,
            assignment.OfficerSubjectId, assignment.Reason), cancellationToken);
        await tx.AddActivityAsync(new ClaimActivity(claim.Id, actor, now, "ClaimAssigned", ActivityVisibility.Staff,
            "Claim assignment updated."), cancellationToken);
        await tx.AddOutboxMessageAsync(new ClaimOutboxMessage(Guid.NewGuid(), claim.Id, "ClaimAssigned", 1, now,
            new Dictionary<string, string> { ["claimId"] = claim.Id.ToString("D"), ["teamId"] = assignment.TeamId,
                ["officerSubjectId"] = assignment.OfficerSubjectId ?? "" }), cancellationToken);
        var version = await tx.CommitAsync(cancellationToken);
        return new ClaimCommandResult(claim.Id, claim.ClaimNumber, claim.Status, version);
    }
}

public sealed class StartReviewHandler(IClaimUnitOfWork uow, ICurrentUserContext user, IClock clock)
    : ClaimCommandHandlerBase(uow, user, clock), ICommandHandler<StartReview, ClaimCommandResult>
{
    public async Task<ClaimCommandResult> HandleAsync(StartReview command, CancellationToken cancellationToken = default)
    {
        var (tx, claim, actor) = await LoadStaffClaimAsync(command.ClaimId, command.ExpectedVersion, cancellationToken);
        await using (tx)
        {
            var now = Clock.UtcNow; claim.StartReview(actor);
            return await FinishAsync(tx, claim, actor, now, "ReviewStarted", ActivityVisibility.Staff,
                "Review started.", "ClaimStatusChanged", Data(("claimId", claim.Id.ToString("D")), ("status", claim.Status.ToString())), cancellationToken);
        }
    }
}

public sealed class RequestInformationHandler(IClaimUnitOfWork uow, ICurrentUserContext user, IClock clock)
    : ClaimCommandHandlerBase(uow, user, clock), ICommandHandler<RequestInformation, ClaimCommandResult>
{
    public async Task<ClaimCommandResult> HandleAsync(RequestInformation command, CancellationToken cancellationToken = default)
    {
        var (tx, claim, actor) = await LoadStaffClaimAsync(command.ClaimId, command.ExpectedVersion, cancellationToken);
        await using (tx)
        {
            var now = Clock.UtcNow; var requestId = claim.RequestInformation(actor, command.Question, now);
            await tx.AddWorkflowRecordAsync(new ClaimInformationRequestHistory(claim.Id, actor, now, requestId, command.Question), cancellationToken);
            return await FinishAsync(tx, claim, actor, now, "InformationRequested", ActivityVisibility.Claimant,
                "Additional information was requested.", "ClaimInformationRequested", Data(("claimId", claim.Id.ToString("D")),
                    ("requestId", requestId.ToString("D"))), cancellationToken);
        }
    }
}

public sealed class SubmitInformationResponseHandler(IClaimUnitOfWork uow, ICurrentUserContext user, IClock clock)
    : ClaimCommandHandlerBase(uow, user, clock), ICommandHandler<SubmitInformationResponse, ClaimCommandResult>
{
    public async Task<ClaimCommandResult> HandleAsync(SubmitInformationResponse command, CancellationToken cancellationToken = default)
    {
        _ = Authorization.RequireVersion(command.ExpectedVersion);
        Authorization.RequireRole(User, ApplicationRole.Claimant);
        var actor = Authorization.RequireSubject(User);
        await using var tx = await UnitOfWork.BeginAsync(cancellationToken);
        var claim = await tx.LoadClaimAsync(command.ClaimId, command.ExpectedVersion, cancellationToken)
            ?? throw new ResourceNotFoundException("Claim was not found.");
        var now = Clock.UtcNow;
        claim.SubmitInformationResponse(command.RequestId, actor, command.ResponseText, now);
        await tx.AddWorkflowRecordAsync(new ClaimInformationResponseHistory(claim.Id, actor, now, command.RequestId, command.ResponseText), cancellationToken);
        return await FinishAsync(tx, claim, actor, now, "InformationResponseSubmitted", ActivityVisibility.Claimant,
            "Information response submitted.", "ClaimInformationResponseSubmitted", Data(("claimId", claim.Id.ToString("D")),
                ("requestId", command.RequestId.ToString("D"))), cancellationToken);
    }
}

public sealed class ResumeReviewHandler(IClaimUnitOfWork uow, ICurrentUserContext user, IClock clock)
    : ClaimCommandHandlerBase(uow, user, clock), ICommandHandler<ResumeReview, ClaimCommandResult>
{
    public async Task<ClaimCommandResult> HandleAsync(ResumeReview command, CancellationToken cancellationToken = default)
    {
        var (tx, claim, actor) = await LoadStaffClaimAsync(command.ClaimId, command.ExpectedVersion, cancellationToken);
        await using (tx)
        {
            var now = Clock.UtcNow; claim.ResumeReview(actor);
            return await FinishAsync(tx, claim, actor, now, "ReviewResumed", ActivityVisibility.Staff,
                "Review resumed.", "ClaimStatusChanged", Data(("claimId", claim.Id.ToString("D")), ("status", claim.Status.ToString())), cancellationToken);
        }
    }
}

public sealed class RecordAssessmentHandler(IClaimUnitOfWork uow, ICurrentUserContext user, IClock clock)
    : ClaimCommandHandlerBase(uow, user, clock), ICommandHandler<RecordAssessment, ClaimCommandResult>
{
    public async Task<ClaimCommandResult> HandleAsync(RecordAssessment command, CancellationToken cancellationToken = default)
    {
        RequireText(command.AssessmentType, nameof(command.AssessmentType));
        RequireText(command.Rationale, nameof(command.Rationale));
        var (tx, claim, actor) = await LoadStaffClaimAsync(command.ClaimId, command.ExpectedVersion, cancellationToken);
        await using (tx)
        {
            var now = Clock.UtcNow;
            var assessmentType = command.AssessmentType.Trim();
            var rationale = command.Rationale.Trim();
            var recommendation = string.IsNullOrWhiteSpace(command.Recommendation) ? null : command.Recommendation.Trim();
            var assessment = new ClaimAssessment(actor, now, assessmentType, rationale, recommendation);
            var reserve = command.ReserveEstimate?.ToDomain();
            claim.RecordAssessment(assessment, reserve);
            await tx.AddWorkflowRecordAsync(new ClaimAssessmentHistory(claim.Id, actor, now, assessmentType, rationale, recommendation), cancellationToken);
            if (reserve is not null)
                await tx.AddWorkflowRecordAsync(new ClaimReserveHistory(claim.Id, actor, now, reserve,
                    string.IsNullOrWhiteSpace(command.ReserveReason) ? "Assessment reserve estimate." : command.ReserveReason.Trim()), cancellationToken);
            return await FinishAsync(tx, claim, actor, now, "AssessmentRecorded", ActivityVisibility.Staff,
                "Assessment recorded.", "ClaimAssessmentRecorded", Data(("claimId", claim.Id.ToString("D")),
                    ("status", claim.Status.ToString())), cancellationToken);
        }
    }
}

public sealed class RecordFinalDecisionHandler(IClaimUnitOfWork uow, ICurrentUserContext user, IClock clock)
    : ClaimCommandHandlerBase(uow, user, clock), ICommandHandler<RecordFinalDecision, ClaimCommandResult>
{
    public async Task<ClaimCommandResult> HandleAsync(RecordFinalDecision command, CancellationToken cancellationToken = default)
    {
        RequireText(command.Reason, nameof(command.Reason));
        var (tx, claim, actor) = await LoadStaffClaimAsync(command.ClaimId, command.ExpectedVersion, cancellationToken);
        await using (tx)
        {
            var now = Clock.UtcNow;
            var reason = command.Reason.Trim();
            claim.RecordDecision(new ClaimDecision(command.Type, reason, actor, now, command.Amount?.ToDomain()));
            await tx.AddWorkflowRecordAsync(new ClaimDecisionHistory(claim.Id, actor, now, command.Type, reason), cancellationToken);
            return await FinishAsync(tx, claim, actor, now, "DecisionRecorded", ActivityVisibility.Staff,
                "A decision was recorded.", "ClaimDecisionRecorded", Data(("claimId", claim.Id.ToString("D")),
                    ("decision", command.Type.ToString()), ("status", claim.Status.ToString())), cancellationToken);
        }
    }
}

public sealed class CompleteSettlementWorkflowHandler(IClaimUnitOfWork uow, ICurrentUserContext user, IClock clock)
    : ClaimCommandHandlerBase(uow, user, clock), ICommandHandler<CompleteSettlementWorkflow, ClaimCommandResult>
{
    public async Task<ClaimCommandResult> HandleAsync(CompleteSettlementWorkflow command, CancellationToken cancellationToken = default)
    {
        var (tx, claim, actor) = await LoadStaffClaimAsync(command.ClaimId, command.ExpectedVersion, cancellationToken);
        await using (tx)
        {
            var now = Clock.UtcNow; var amount = command.Amount?.ToDomain();
            claim.CompleteSettlement(new ClaimSettlement(actor, now, amount, command.Reference));
            await tx.AddWorkflowRecordAsync(new ClaimSettlementHistory(claim.Id, actor, now, amount, command.Reference), cancellationToken);
            return await FinishAsync(tx, claim, actor, now, "SettlementWorkflowCompleted", ActivityVisibility.Staff,
                "Settlement workflow completed; no payment was executed.", "ClaimSettlementCompleted", Data(("claimId", claim.Id.ToString("D")),
                    ("status", claim.Status.ToString())), cancellationToken);
        }
    }
}

public sealed class AddInternalNoteHandler(IClaimUnitOfWork uow, ICurrentUserContext user, IClock clock)
    : ClaimCommandHandlerBase(uow, user, clock), ICommandHandler<AddInternalNote, ClaimCommandResult>
{
    public async Task<ClaimCommandResult> HandleAsync(AddInternalNote command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.NoteText)) throw new ValidationException("Note text is required.");
        var (tx, claim, actor) = await LoadStaffClaimAsync(command.ClaimId, command.ExpectedVersion, cancellationToken);
        await using (tx)
        {
            var now = Clock.UtcNow;
            await tx.AddWorkflowRecordAsync(new ClaimInternalNote(claim.Id, actor, now, command.NoteText.Trim()), cancellationToken);
            return await FinishAsync(tx, claim, actor, now, "InternalNoteAdded", ActivityVisibility.Staff,
                "An internal note was added.", "ClaimInternalNoteAdded", Data(("claimId", claim.Id.ToString("D"))), cancellationToken);
        }
    }

}
