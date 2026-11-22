using Chubb.Claims.Application.Abstractions;
using Chubb.Claims.Application.Contracts;
using Chubb.Claims.Application.Queries;
using Chubb.Claims.Domain.Claims;
using Microsoft.EntityFrameworkCore;

namespace Chubb.Claims.Infrastructure.Persistence;

public sealed class SqlClaimQueryStore(ClaimsDbContext db) : IClaimQueryStore
{
    public async Task<ClaimantClaimRecord?> FindClaimantClaimAsync(Guid claimId, CancellationToken cancellationToken = default)
    {
        var row = await BaseClaims().SingleOrDefaultAsync(x => x.ClaimId == claimId, cancellationToken);
        return row is null ? null : new(row.ClaimantSubjectId, ToClaimantCard(row), ToClaimantDetail(row));
    }

    public async Task<PageResult<ClaimantClaimRecord>> ListClaimantClaimsAsync(string claimantSubjectId, PageRequest page, CancellationToken cancellationToken = default)
    {
        var query = BaseClaims().Where(x => x.ClaimantSubjectId == claimantSubjectId);
        if (page.After is { } after) query = query.Where(x => x.CreatedUtc < after.SortTime || (x.CreatedUtc == after.SortTime && x.ClaimId.CompareTo(after.Id) < 0));
        var rows = await query.OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.ClaimId).Take(page.EffectivePageSize + 1).ToListAsync(cancellationToken);
        return Page(rows, page, x => new ClaimantClaimRecord(x.ClaimantSubjectId, ToClaimantCard(x)));
    }

    public async Task<PageResult<ClaimantHistoryRecord>> ListClaimantHistoryAsync(Guid claimId, PageRequest page, CancellationToken cancellationToken = default)
    {
        var owner = await db.Claims.Where(x => x.ClaimId == claimId).Select(x => x.ClaimantSubjectId).SingleOrDefaultAsync(cancellationToken);
        if (owner is null) return new([], null);
        var q = db.ClaimActivities.AsNoTracking().Where(x => x.ClaimId == claimId && x.Visibility != ActivityVisibility.Staff);
        if (page.After is { } after) q = q.Where(x => x.OccurredUtc < after.SortTime || (x.OccurredUtc == after.SortTime && x.ActivityId.CompareTo(after.Id) < 0));
        var rows = await q.OrderByDescending(x => x.OccurredUtc).ThenByDescending(x => x.ActivityId).Take(page.EffectivePageSize + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > page.EffectivePageSize; if (hasMore) rows.RemoveAt(rows.Count - 1);
        var items = rows.Select(x => new ClaimantHistoryRecord(owner, new(x.OccurredUtc, x.ActivityType, x.Summary))).ToArray();
        return new(items, hasMore && rows.Count > 0 ? new PageCursor(rows[^1].OccurredUtc, rows[^1].ActivityId) : null);
    }

    public async Task<StaffClaimRecord?> FindStaffClaimAsync(Guid claimId, IReadOnlySet<string> teamIds, CancellationToken cancellationToken = default)
    {
        var guids = ParseTeams(teamIds);
        var row = await StaffClaims(guids).SingleOrDefaultAsync(x => x.ClaimId == claimId, cancellationToken);
        return row is null ? null : ToStaffRecord(row, true);
    }

    public async Task<PageResult<StaffQueueRecord>> ListStaffQueueAsync(WorkQueueFilter filter, IReadOnlySet<string> teamIds, PageRequest page, CancellationToken cancellationToken = default)
    {
        var guids = ParseTeams(teamIds);
        var q = StaffClaims(guids);
        if (!string.IsNullOrWhiteSpace(filter.MarketCode)) q = q.Where(x => x.MarketCode == filter.MarketCode);
        if (filter.Statuses is { Count: > 0 } statuses) q = q.Where(x => statuses.Contains(x.Status));
        if (Guid.TryParse(filter.TeamId, out var teamId)) q = q.Where(x => x.Assignments.Any(a => a.UnassignedUtc == null && a.TeamId == teamId));
        if (!string.IsNullOrWhiteSpace(filter.OfficerSubjectId)) q = q.Where(x => x.Assignments.Any(a => a.UnassignedUtc == null && a.OfficerSubjectId == filter.OfficerSubjectId));
        if (filter.SubmittedFromUtc is { } from) q = q.Where(x => x.SubmittedUtc >= from);
        if (filter.SubmittedToUtc is { } to) q = q.Where(x => x.SubmittedUtc < to);
        if (filter.IncidentFromUtc is { } incidentFrom) q = q.Where(x => x.IncidentOccurredUtc >= incidentFrom);
        if (filter.IncidentToUtc is { } incidentTo) q = q.Where(x => x.IncidentOccurredUtc < incidentTo);
        if (!string.IsNullOrWhiteSpace(filter.SearchText)) { var term = filter.SearchText.Trim(); q = q.Where(x => x.ClaimNumber.Contains(term)); }
        if (page.After is { } after) q = q.Where(x => x.CreatedUtc < after.SortTime || (x.CreatedUtc == after.SortTime && x.ClaimId.CompareTo(after.Id) < 0));
        var rows = await q.OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.ClaimId).Take(page.EffectivePageSize + 1).ToListAsync(cancellationToken);
        return Page(rows, page, x => { var rec = ToStaffRecord(x, false); return new StaffQueueRecord(rec.TeamId, rec.Card); });
    }

    public async Task<ManagerWorkloadSummary> GetManagerWorkloadAsync(IReadOnlySet<string> teamIds, IReadOnlySet<string> marketCodes, CancellationToken cancellationToken = default)
    {
        var teams = ParseTeams(teamIds); var now = DateTimeOffset.UtcNow;
        var included = ManagerReportingRules.CurrentStatuses.ToArray();
        var rows = await db.Claims.AsNoTracking().Where(c => marketCodes.Contains(c.MarketCode) && included.Contains(c.Status)
                && (!c.Assignments.Any(a => a.UnassignedUtc == null) || c.Assignments.Any(a => a.UnassignedUtc == null && teams.Contains(a.TeamId))))
            .Select(c => new { c.MarketCode, c.Status, c.SubmittedUtc, TeamId = c.Assignments.Where(a => a.UnassignedUtc == null).Select(a => (Guid?)a.TeamId).FirstOrDefault(), Officer = c.Assignments.Where(a => a.UnassignedUtc == null).Select(a => a.OfficerSubjectId).FirstOrDefault() })
            .ToListAsync(cancellationToken);
        var groups = rows.GroupBy(x => (x.MarketCode, Team: x.TeamId == null ? null : x.TeamId.Value.ToString("D"), x.Officer))
            .Select(g => new WorkloadGroup(g.Key.MarketCode, g.Key.Team, g.Key.Officer,
                g.GroupBy(x => x.Status).Select(s => new StatusCount(s.Key, s.Count())).ToArray(),
                g.Count(x => x.Officer is null),
                new Dictionary<string, int> { ["0-7 days"] = g.Count(x => ManagerReportingRules.AgeBand(now, x.SubmittedUtc) == "0-7 days"), ["8-30 days"] = g.Count(x => ManagerReportingRules.AgeBand(now, x.SubmittedUtc) == "8-30 days"), ["31-90 days"] = g.Count(x => ManagerReportingRules.AgeBand(now, x.SubmittedUtc) == "31-90 days"), ["90+ days"] = g.Count(x => ManagerReportingRules.AgeBand(now, x.SubmittedUtc) == "90+ days") })).ToArray();
        return new(now, groups, "Included current statuses: Submitted, InReview, Assessment, AwaitingInformation, Approved. Excluded: Rejected, Settled. Elapsed UTC days since submission: 0-7, 8-30, 31-90, 90+; demo reporting bands, not SLAs.");
    }

    public async Task<ManagerPerformanceSummary> GetManagerPerformanceAsync(IReadOnlySet<string> teamIds, IReadOnlySet<string> marketCodes, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var teams = ParseTeams(teamIds);
        var decisions = await db.ClaimDecisions.AsNoTracking().Where(d => d.DecidedUtc >= fromUtc && d.DecidedUtc < toUtc && marketCodes.Contains(d.Claim.MarketCode)
                && d.Claim.Assignments.Any(a => teams.Contains(a.TeamId) && a.AssignedUtc <= d.DecidedUtc && (a.UnassignedUtc == null || a.UnassignedUtc > d.DecidedUtc)))
            .Select(d => new { d.DecidedUtc, d.Claim.SubmittedUtc }).ToListAsync(cancellationToken);
        var settlements = await db.ClaimSettlements.AsNoTracking().Where(s => s.CompletedUtc >= fromUtc && s.CompletedUtc < toUtc && marketCodes.Contains(s.Claim.MarketCode)
                && s.Claim.Assignments.Any(a => teams.Contains(a.TeamId) && a.AssignedUtc <= s.CompletedUtc && (a.UnassignedUtc == null || a.UnassignedUtc > s.CompletedUtc)))
            .Select(s => new { CompletedUtc = s.CompletedUtc!.Value, s.Claim.SubmittedUtc }).ToListAsync(cancellationToken);
        var dDays = decisions.Select(x => (x.DecidedUtc - x.SubmittedUtc).TotalDays).ToArray(); var sDays = settlements.Select(x => (x.CompletedUtc - x.SubmittedUtc).TotalDays).ToArray();
        return new(fromUtc, toUtc, decisions.Count, Avg(dDays), Median(dDays), 0, settlements.Count, Avg(sDays), 0);
    }

    public async Task<ManagerExposureSummary> GetManagerExposureAsync(IReadOnlySet<string> teamIds, IReadOnlySet<string> marketCodes, CancellationToken cancellationToken = default)
    {
        var included = ManagerReportingRules.CurrentStatuses.ToArray();
        var teams = ParseTeams(teamIds); var now = DateTimeOffset.UtcNow;
        var query = db.Claims.AsNoTracking().Where(x => marketCodes.Contains(x.MarketCode) && included.Contains(x.Status)
            && (!x.Assignments.Any(a => a.UnassignedUtc == null) || x.Assignments.Any(a => a.UnassignedUtc == null && teams.Contains(a.TeamId))));
        var aggregates = await query.Where(x => x.CurrentReserveAmount != null && x.CurrentReserveCurrency != null)
            .GroupBy(x => new { x.MarketCode, Currency = x.CurrentReserveCurrency! })
            .Select(g => new ExposureGroup(g.Key.MarketCode, g.Key.Currency, g.Sum(x => x.CurrentReserveAmount!.Value), g.Count()))
            .ToArrayAsync(cancellationToken);
        var missing = await query.CountAsync(x => x.CurrentReserveAmount == null || x.CurrentReserveCurrency == null, cancellationToken);
        return new(now, aggregates, missing, included, "Operational estimate from current reserve estimates only; included statuses: Submitted, InReview, Assessment, AwaitingInformation, Approved; excluded: Rejected, Settled. Grouped separately by market and currency; no FX conversion or booked liability. Demo status scope.");
    }

    private IQueryable<ClaimRow> BaseClaims() => db.Claims.AsNoTracking().Include(x => x.MotorDetails).Include(x => x.PropertyDetails).Include(x => x.Assignments.Where(a => a.UnassignedUtc == null)).Include(x => x.InformationRequests).ThenInclude(x => x.Responses).Include(x => x.Decision).Include(x => x.Activities.Where(a => a.Visibility != ActivityVisibility.Staff));
    private IQueryable<ClaimRow> StaffClaims(Guid[] teams) => db.Claims.AsNoTracking().Include(x => x.MotorDetails).Include(x => x.PropertyDetails).Include(x => x.Assignments).Include(x => x.Decision).Include(x => x.Assessments).Include(x => x.ReserveHistory).Include(x => x.Notes).Include(x => x.Activities).Where(x => x.Assignments.Any(a => a.UnassignedUtc == null && teams.Contains(a.TeamId)));
    private static ClaimAssignmentRow? Active(ClaimRow x) => x.Assignments.SingleOrDefault(a => a.UnassignedUtc == null);
    private static Guid[] ParseTeams(IReadOnlySet<string> teams) => teams.Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).ToArray();
    private static double? Avg(double[] v) => v.Length == 0 ? null : v.Average();
    private static double? Median(double[] v) { if (v.Length == 0) return null; var s = v.Order().ToArray(); return s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2; }
    private static string Version(ClaimRow x) => Convert.ToBase64String(x.RowVersion);
    private static ClaimantClaimCard ToClaimantCard(ClaimRow x) => new(x.ClaimId, x.ClaimNumber, x.MarketCode, x.LineOfBusiness, x.Status, x.SubmittedUtc, Version(x));
    private static ClaimantClaimDetail ToClaimantDetail(ClaimRow x) => new(x.ClaimId, x.ClaimNumber, x.MarketCode, x.LineOfBusiness, x.Status, x.ReportedUtc, x.SubmittedUtc,
        new(x.ClaimantName, x.ClaimantEmail, x.ClaimantPhone), new(x.IncidentOccurredUtc, x.Description), ToDetails(x),
        x.Decision is null ? null : new(x.Decision.DecisionType, x.Decision.DecidedUtc, x.Decision.DecisionAmount is decimal a && x.Decision.DecisionCurrency is string c ? new(a, c) : null),
        x.InformationRequests.Select(r => new ClaimantInformationRequestDto(r.RequestId, r.Question, r.RequestedUtc, r.Status == "Open", r.Responses.OrderByDescending(a => a.SubmittedUtc).Select(a => (DateTimeOffset?)a.SubmittedUtc).FirstOrDefault())).ToArray(),
        x.Activities.Select(a => new ClaimantActivityDto(a.OccurredUtc, a.ActivityType, a.Summary)).ToArray(), Version(x));
    private static ClaimDetailsDto ToDetails(ClaimRow x) => x.LineOfBusiness == LineOfBusiness.Motor && x.MotorDetails is { } m
        ? new MotorClaimDetailsDto(m.VehicleRegistration, m.Make, m.Model, m.ModelYear, m.VehicleUse, m.Drivable, m.OtherPartyInvolved)
        : x.PropertyDetails is { } p ? new PropertyClaimDetailsDto(p.PropertyType, p.AddressLine1, p.AddressLine2, p.City, p.Region, p.PostalCode, p.CountryCode, p.LossCause, p.AffectedAreaDescription, p.Habitable)
        : throw new InvalidOperationException("Claim details row is missing.");
    private static StaffClaimRecord ToStaffRecord(ClaimRow x, bool detail)
    {
        var assignment = Active(x) ?? throw new InvalidOperationException("A staff claim row requires an active assignment.");
        var reserve = x.CurrentReserveAmount is decimal amount && x.CurrentReserveCurrency is string currency ? new MoneyEstimateDto(amount, currency) : null;
        var card = new StaffClaimCard(x.ClaimId, x.ClaimNumber, x.MarketCode, x.LineOfBusiness, x.Status, x.SubmittedUtc, assignment.TeamId.ToString("D"), assignment.OfficerSubjectId, reserve, Version(x));
        StaffClaimDetail? item = detail ? new(x.ClaimId, x.ClaimNumber, x.MarketCode, x.LineOfBusiness, x.Status, x.ReportedUtc, x.SubmittedUtc, x.ClaimantSubjectId,
            new(x.ClaimantName, x.ClaimantEmail, x.ClaimantPhone), new(x.IncidentOccurredUtc, x.Description), ToDetails(x), assignment.TeamId.ToString("D"), assignment.OfficerSubjectId, reserve,
            x.Decision is null ? null : new(x.Decision.DecisionType, x.Decision.Reason, x.Decision.DecidedBySubjectId, x.Decision.DecidedUtc, x.Decision.DecisionAmount is decimal a && x.Decision.DecisionCurrency is string c ? new(a, c) : null),
            x.Assessments.Select(a => new StaffAssessmentDto(a.CreatedUtc, a.AssessorSubjectId, a.AssessmentType, a.Rationale, a.Recommendation)).ToArray(),
            x.ReserveHistory.OrderByDescending(r => r.RecordedUtc).Select(r => new MoneyEstimateDto(r.Amount, r.Currency)).ToArray(),
            x.Notes.Where(n => n.DeletedUtc == null).Select(n => new StaffNoteDto(n.CreatedUtc, n.AuthorSubjectId, n.NoteText)).ToArray(),
            x.Activities.Select(a => new StaffActivityDto(a.OccurredUtc, a.ActivityType, a.Summary, a.Visibility)).ToArray(), Version(x),
            x.Assignments.OrderBy(a => a.AssignedUtc).Select(a => new StaffAssignmentDto(a.TeamId.ToString("D"), a.OfficerSubjectId, a.AssignedBySubjectId, a.AssignedUtc, a.UnassignedUtc, a.Reason)).ToArray()) : null;
        return new(assignment.TeamId.ToString("D"), card, item);
    }
    private static PageResult<T> Page<T>(List<ClaimRow> rows, PageRequest page, Func<ClaimRow, T> map)
    {
        var hasMore = rows.Count > page.EffectivePageSize; if (hasMore) rows.RemoveAt(rows.Count - 1);
        var items = rows.Select(map).ToArray(); var next = hasMore && rows.Count > 0 ? new PageCursor(rows[^1].CreatedUtc, rows[^1].ClaimId) : null;
        return new(items, next);
    }
}
