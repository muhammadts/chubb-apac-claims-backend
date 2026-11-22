using System.Text.Json;
using Chubb.Claims.Application.Abstractions;
using Chubb.Claims.Domain.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Chubb.Claims.Infrastructure.Persistence;

public sealed class SqlClaimUnitOfWork(ClaimsDbContext db) : IClaimUnitOfWork
{
    public async Task<IClaimWriteTransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        return new SqlClaimWriteTransaction(db, transaction);
    }
}

internal sealed class SqlClaimWriteTransaction(ClaimsDbContext db, IDbContextTransaction transaction) : IClaimWriteTransaction
{
    private readonly List<ClaimWorkflowRecord> _workflow = [];
    private readonly List<ClaimActivity> _activities = [];
    private readonly List<ClaimOutboxMessage> _outbox = [];
    private Claim? _aggregate;
    private ClaimRow? _row;
    private bool _completed;

    public async Task<Claim?> LoadClaimAsync(Guid claimId, string expectedVersion, CancellationToken cancellationToken = default)
    {
        _row = await db.Claims.Include(x => x.MotorDetails).Include(x => x.PropertyDetails)
            .Include(x => x.Assignments.Where(a => a.UnassignedUtc == null))
            .Include(x => x.InformationRequests).ThenInclude(x => x.Responses)
            .Include(x => x.Assessments).Include(x => x.ReserveHistory).Include(x => x.Decision).Include(x => x.Settlement)
            .SingleOrDefaultAsync(x => x.ClaimId == claimId, cancellationToken);
        if (_row is null) return null;
        var version = Convert.ToBase64String(_row.RowVersion);
        if (!string.Equals(version, expectedVersion, StringComparison.Ordinal))
            throw new ConcurrencyConflictException("The claim has changed; reload it before retrying.");
        var active = _row.Assignments.SingleOrDefault();
        ClaimDetails details = _row.LineOfBusiness switch
        {
            LineOfBusiness.Motor when _row.MotorDetails is not null => new MotorClaimDetails(_row.MotorDetails.VehicleRegistration, _row.MotorDetails.Make, _row.MotorDetails.Model, _row.MotorDetails.ModelYear, _row.MotorDetails.VehicleUse, _row.MotorDetails.Drivable, _row.MotorDetails.OtherPartyInvolved),
            LineOfBusiness.Property when _row.PropertyDetails is not null => new PropertyClaimDetails(_row.PropertyDetails.PropertyType, _row.PropertyDetails.AddressLine1, _row.PropertyDetails.AddressLine2, _row.PropertyDetails.City, _row.PropertyDetails.Region, _row.PropertyDetails.PostalCode, _row.PropertyDetails.CountryCode, _row.PropertyDetails.LossCause, _row.PropertyDetails.AffectedAreaDescription, _row.PropertyDetails.Habitable),
            _ => throw new InvalidOperationException("Claim detail row does not match its line of business.")
        };
        var requests = _row.InformationRequests.Select(x => ClaimInformationRequest.Rehydrate(x.RequestId, x.RequestedBySubjectId, x.Question, x.RequestedUtc,
            x.Responses.OrderByDescending(r => r.SubmittedUtc).Select(r => new ClaimInformationResponse(r.SubmittedBySubjectId, r.SubmittedUtc, r.ResponseText)).FirstOrDefault()));
        var assessments = _row.Assessments.Select(x => new ClaimAssessment(x.AssessorSubjectId, x.CreatedUtc, x.AssessmentType, x.Rationale, x.Recommendation));
        var reserve = _row.CurrentReserveAmount is decimal amount && _row.CurrentReserveCurrency is string currency ? new MoneyEstimate(amount, currency) : null;
        _aggregate = Claim.Rehydrate(_row.ClaimId, _row.ClaimNumber, _row.ClaimantSubjectId,
            new ClaimantContactSnapshot(_row.ClaimantName, _row.ClaimantEmail, _row.ClaimantPhone), _row.MarketCode,
            _row.LineOfBusiness, new IncidentFacts(_row.IncidentOccurredUtc, _row.Description), details,
            _row.ReportedUtc, _row.SubmittedUtc, _row.Status,
            active is null ? null : new ClaimAssignment(active.TeamId.ToString("D"), active.OfficerSubjectId, active.AssignedBySubjectId, active.AssignedUtc, active.Reason),
            requests, assessments, reserve,
            _row.Decision is null ? null : new ClaimDecision(_row.Decision.DecisionType, _row.Decision.Reason, _row.Decision.DecidedBySubjectId, _row.Decision.DecidedUtc,
                _row.Decision.DecisionAmount is decimal da && _row.Decision.DecisionCurrency is string dc ? new MoneyEstimate(da, dc) : null),
            _row.Settlement is null ? null : new ClaimSettlement(_row.Settlement.RecordedBySubjectId, _row.Settlement.CompletedUtc ?? _row.Settlement.CreatedUtc,
                _row.Settlement.Amount is decimal sa && _row.Settlement.Currency is string sc ? new MoneyEstimate(sa, sc) : null, _row.Settlement.Reference));
        return _aggregate;
    }

    public async Task AddClaimAsync(Claim claim, CancellationToken cancellationToken = default)
    {
        _aggregate = claim;
        _row = new ClaimRow { ClaimId = claim.Id, ClaimNumber = claim.ClaimNumber, ClaimantSubjectId = claim.ClaimantSubjectId,
            ClaimantName = claim.ClaimantContact.Name, ClaimantEmail = claim.ClaimantContact.Email, ClaimantPhone = claim.ClaimantContact.Phone,
            MarketCode = claim.MarketCode, LineOfBusiness = claim.LineOfBusiness, IncidentOccurredUtc = claim.Incident.OccurredAt,
            ReportedUtc = claim.ReportedAt, Description = claim.Incident.Description, Status = claim.Status,
            SubmittedUtc = claim.SubmittedAt, CreatedUtc = claim.SubmittedAt, UpdatedUtc = claim.SubmittedAt };
        if (claim.Details is MotorClaimDetails motor) _row.MotorDetails = new MotorClaimDetailsRow { ClaimId = claim.Id, VehicleRegistration = motor.VehicleRegistration, Make = motor.Make, Model = motor.Model, ModelYear = motor.ModelYear, VehicleUse = motor.VehicleUse, Drivable = motor.Drivable, OtherPartyInvolved = motor.OtherPartyInvolved };
        if (claim.Details is PropertyClaimDetails property) _row.PropertyDetails = new PropertyClaimDetailsRow { ClaimId = claim.Id, PropertyType = property.PropertyType, AddressLine1 = property.AddressLine1, AddressLine2 = property.AddressLine2, City = property.City, Region = property.Region, PostalCode = property.PostalCode, CountryCode = property.CountryCode, LossCause = property.LossCause, AffectedAreaDescription = property.AffectedAreaDescription, Habitable = property.Habitable };
        if (!await db.ClaimantProfiles.AnyAsync(x => x.SubjectId == claim.ClaimantSubjectId, cancellationToken))
            db.ClaimantProfiles.Add(new ClaimantProfileRow { SubjectId = claim.ClaimantSubjectId, DisplayName = claim.ClaimantContact.Name, Email = claim.ClaimantContact.Email, Phone = claim.ClaimantContact.Phone, CreatedUtc = claim.SubmittedAt, UpdatedUtc = claim.SubmittedAt });
        db.Claims.Add(_row);
    }
    public Task AddWorkflowRecordAsync(ClaimWorkflowRecord record, CancellationToken cancellationToken = default) { _workflow.Add(record); return Task.CompletedTask; }
    public Task AddActivityAsync(ClaimActivity activity, CancellationToken cancellationToken = default) { _activities.Add(activity); return Task.CompletedTask; }
    public Task AddOutboxMessageAsync(ClaimOutboxMessage message, CancellationToken cancellationToken = default) { _outbox.Add(message); return Task.CompletedTask; }

    public async Task<string> CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_aggregate is null || _row is null) throw new InvalidOperationException("No claim is staged in this transaction.");
        _row.Status = _aggregate.Status; _row.UpdatedUtc = DateTimeOffset.UtcNow;
        if (_aggregate.ReserveEstimate is { } estimate) { _row.CurrentReserveAmount = estimate.Amount; _row.CurrentReserveCurrency = estimate.Currency; }
        if (_aggregate.Status is ClaimStatus.Rejected or ClaimStatus.Settled) _row.ClosedUtc = _row.UpdatedUtc;
        foreach (var record in _workflow) Persist(record);
        foreach (var activity in _activities) db.ClaimActivities.Add(new ClaimActivityRow { ActivityId = Guid.NewGuid(), ClaimId = activity.ClaimId, ActivityType = activity.ActivityType, ActorSubjectId = activity.ActorSubjectId, ActorKind = "User", Visibility = activity.Visibility, Summary = activity.Summary, OccurredUtc = activity.OccurredAt });
        foreach (var message in _outbox) db.OutboxMessages.Add(new OutboxMessageRow { EventId = message.EventId, AggregateId = message.ClaimId, EventType = message.EventType, EventVersion = checked((short)message.EventVersion), OccurredUtc = message.OccurredAt, CreatedUtc = DateTimeOffset.UtcNow, NextAttemptUtc = message.OccurredAt, Payload = JsonSerializer.Serialize(message.Data) });
        try { await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); _completed = true; }
        catch (DbUpdateConcurrencyException ex) { await transaction.RollbackAsync(cancellationToken); throw new ConcurrencyConflictException($"The claim was changed by another writer: {ex.Message}"); }
        return Convert.ToBase64String(_row.RowVersion);
    }

    private void Persist(ClaimWorkflowRecord record)
    {
        switch (record)
        {
            case ClaimAssignmentHistory a:
                var active = _row!.Assignments.SingleOrDefault(x => x.UnassignedUtc == null);
                if (active is not null) active.UnassignedUtc = a.OccurredAt;
                var teamId = Guid.Parse(a.TeamId); var assignment = new ClaimAssignmentRow { AssignmentId = Guid.NewGuid(), ClaimId = a.ClaimId, TeamId = teamId, OfficerSubjectId = a.OfficerSubjectId, AssignedBySubjectId = a.ActorSubjectId, AssignedUtc = a.OccurredAt, Reason = a.Reason };
                db.ClaimAssignments.Add(assignment); if (!_row.Assignments.Contains(assignment)) _row.Assignments.Add(assignment); break;
            case ClaimAssessmentHistory a: db.ClaimAssessments.Add(new ClaimAssessmentRow { AssessmentId = Guid.NewGuid(), ClaimId = a.ClaimId, AssessorSubjectId = a.ActorSubjectId, AssessmentType = a.AssessmentType, Rationale = a.Rationale, Recommendation = a.Recommendation, CreatedUtc = a.OccurredAt }); break;
            case ClaimInformationRequestHistory r: db.ClaimInformationRequests.Add(new ClaimInformationRequestRow { RequestId = r.RequestId, ClaimId = r.ClaimId, RequestedBySubjectId = r.ActorSubjectId, Question = r.Question, RequestedUtc = r.OccurredAt, Status = "Open" }); break;
            case ClaimInformationResponseHistory r:
                var request = _row!.InformationRequests.SingleOrDefault(x => x.RequestId == r.RequestId) ?? db.ClaimInformationRequests.Local.SingleOrDefault(x => x.RequestId == r.RequestId);
                if (request is null) throw new InvalidOperationException("Information request is missing from the claim transaction.");
                request.Status = "Responded"; request.ResolvedUtc = r.OccurredAt;
                db.ClaimInformationResponses.Add(new ClaimInformationResponseRow { ResponseId = Guid.NewGuid(), RequestId = r.RequestId, SubmittedBySubjectId = r.ActorSubjectId, ResponseText = r.ResponseText, SubmittedUtc = r.OccurredAt }); break;
            case ClaimReserveHistory r: db.ClaimReserveHistory.Add(new ClaimReserveHistoryRow { ReserveEntryId = Guid.NewGuid(), ClaimId = r.ClaimId, Amount = r.Estimate.Amount, Currency = r.Estimate.Currency, EstimateType = "Estimate", Reason = r.Reason, RecordedBySubjectId = r.ActorSubjectId, RecordedUtc = r.OccurredAt }); break;
            case ClaimDecisionHistory d: db.ClaimDecisions.Add(new ClaimDecisionRow { DecisionId = Guid.NewGuid(), ClaimId = d.ClaimId, DecisionType = d.Type, Reason = d.Reason, DecisionAmount = _aggregate?.Decision?.Amount?.Amount, DecisionCurrency = _aggregate?.Decision?.Amount?.Currency, DecidedBySubjectId = d.ActorSubjectId, DecidedUtc = d.OccurredAt }); break;
            case ClaimSettlementHistory s: db.ClaimSettlements.Add(new ClaimSettlementRow { SettlementId = Guid.NewGuid(), ClaimId = s.ClaimId, Status = "Completed", Amount = s.Amount?.Amount, Currency = s.Amount?.Currency, RecordedBySubjectId = s.ActorSubjectId, CreatedUtc = s.OccurredAt, CompletedUtc = s.OccurredAt, Reference = s.Reference }); break;
            case ClaimInternalNote n: db.ClaimNotes.Add(new ClaimNoteRow { NoteId = Guid.NewGuid(), ClaimId = n.ClaimId, AuthorSubjectId = n.ActorSubjectId, NoteText = n.NoteText, CreatedUtc = n.OccurredAt }); break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_completed) await transaction.RollbackAsync();
        await transaction.DisposeAsync();
    }
}
