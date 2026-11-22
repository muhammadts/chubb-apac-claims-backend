using Chubb.Claims.Application.Abstractions;
using Chubb.Claims.Application.Commands;
using Chubb.Claims.Application.Contracts;
using Chubb.Claims.Application.Queries;
using Chubb.Claims.Domain.Claims;

namespace Chubb.Claims.Application.Tests;

public sealed class ApplicationHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SubmitClaimCommitsClaimActivityAndOutboxTogether()
    {
        var user = User("claimant-1", ApplicationRole.Claimant);
        var uow = new InMemoryUnitOfWork();
        var handler = new SubmitClaimHandler(uow, user, new ClaimNumbers(), new FixedClock(Now));
        var claimId = Guid.NewGuid();

        var result = await handler.HandleAsync(new SubmitClaim(claimId, "XXX", LineOfBusiness.Motor,
            new ClaimantContactInput("Demo Claimant", "demo@example.test", null),
            new IncidentInput(Now.AddDays(-1), "Synthetic test incident."), new MotorClaimDetailsInput(), Now.AddHours(-1)));

        Assert.Equal(claimId, result.ClaimId);
        Assert.Equal(ClaimStatus.Submitted, result.Status);
        Assert.Single(uow.Claims);
        Assert.Single(uow.Activities);
        Assert.Single(uow.OutboxMessages);
        Assert.Equal(1, uow.CommitCount);
        Assert.Equal(uow.Activities.Single().ClaimId, uow.OutboxMessages.Single().ClaimId);
        Assert.Equal("ClaimSubmitted", uow.OutboxMessages.Single().EventType);
        Assert.DoesNotContain("claimant@example.test", string.Join(";", uow.OutboxMessages.Single().Data.Values));
    }

    [Fact]
    public async Task StaffMutationRejectsClaimOutsideCurrentTeamScope()
    {
        var claim = CreateClaim();
        claim.Assign(new ClaimAssignment("team-a", "officer-a", "manager-a", Now));
        var uow = new InMemoryUnitOfWork(claim);
        var user = User("officer-b", ApplicationRole.ClaimsOfficer, "team-b");
        var handler = new StartReviewHandler(uow, user, new FixedClock(Now));

        await Assert.ThrowsAsync<AccessDeniedException>(() => handler.HandleAsync(new StartReview(claim.Id, "v1")));

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Empty(uow.Activities);
        Assert.Empty(uow.OutboxMessages);
        Assert.Equal(0, uow.CommitCount);
    }

    [Fact]
    public async Task AssessmentReserveActivityAndIntegrationEventShareOneCommit()
    {
        var claim = CreateClaim();
        claim.Assign(new ClaimAssignment("team-a", "officer-1", "manager-a", Now));
        claim.StartReview("officer-1");
        var uow = new InMemoryUnitOfWork(claim);
        var handler = new RecordAssessmentHandler(uow, User("officer-1", ApplicationRole.ClaimsOfficer, "team-a"), new FixedClock(Now));

        var result = await handler.HandleAsync(new RecordAssessment(claim.Id, "v1", "Initial", "Synthetic rationale.",
            "Review", new MoneyEstimateInput(125m, "USD"), "Demo estimate."));

        Assert.Equal(ClaimStatus.Assessment, result.Status);
        Assert.Equal(1, uow.CommitCount);
        Assert.Equal(2, uow.WorkflowRecords.Count);
        Assert.Equal("AssessmentRecorded", uow.Activities.Single().ActivityType);
        Assert.Equal("ClaimAssessmentRecorded", uow.OutboxMessages.Single().EventType);
        Assert.DoesNotContain("Synthetic rationale.", string.Join(";", uow.OutboxMessages.Single().Data.Values));
    }

    [Fact]
    public async Task ClaimantResponseUsesAuthenticatedSubjectAndRejectsAnotherOwner()
    {
        var claim = CreateClaim();
        claim.StartReview("officer-1");
        var requestId = claim.RequestInformation("officer-1", "Please clarify the loss.", Now);
        var uow = new InMemoryUnitOfWork(claim);
        var handler = new SubmitInformationResponseHandler(uow, User("intruder", ApplicationRole.Claimant), new FixedClock(Now));

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => handler.HandleAsync(
            new SubmitInformationResponse(claim.Id, "v1", requestId, "Response text")));

        Assert.Null(claim.InformationRequests.Single().Response);
        Assert.Empty(uow.Activities);
        Assert.Empty(uow.OutboxMessages);
        Assert.Equal(0, uow.CommitCount);
    }

    [Fact]
    public async Task ClaimantReadReturnsNotFoundForUnownedClaim()
    {
        var user = User("claimant-other", ApplicationRole.Claimant);
        var store = new FakeQueryStore
        {
            ClaimantClaim = new ClaimantClaimRecord("claimant-owner", Card(), SafeDetail())
        };
        var handler = new GetClaimantClaimHandler(store, user);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => handler.HandleAsync(new GetClaimantClaim(Guid.NewGuid())));
    }

    [Fact]
    public void ClaimantAndStaffDetailContractsKeepSensitiveFieldsSeparated()
    {
        var claimantProperties = typeof(ClaimantClaimDetail).GetProperties().Select(property => property.Name).ToHashSet();
        var staffProperties = typeof(StaffClaimDetail).GetProperties().Select(property => property.Name).ToHashSet();

        Assert.DoesNotContain("InternalNotes", claimantProperties);
        Assert.DoesNotContain("Assessments", claimantProperties);
        Assert.DoesNotContain("DecisionRationale", claimantProperties);
        Assert.Contains("InternalNotes", staffProperties);
        Assert.Contains("Assessments", staffProperties);
        Assert.Contains("Decision", staffProperties);
    }

    [Fact]
    public async Task StaffLifecycleReturnsConflictForInvalidTransitionAndStaleVersion()
    {
        var claim = CreateClaim();
        claim.Assign(new ClaimAssignment("team-a", "officer-a", "manager-a", Now));
        claim.StartReview("officer-a");
        var unit = new InMemoryUnitOfWork(claim);
        var handler = new StartReviewHandler(unit, User("officer-a", ApplicationRole.ClaimsOfficer, "team-a"), new FixedClock(Now));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new StartReview(claim.Id, "v1")));
        Assert.Equal(0, unit.CommitCount);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => handler.HandleAsync(new StartReview(claim.Id, "stale")));
        Assert.Equal(0, unit.CommitCount);
    }

    [Fact]
    public async Task AssignmentRequiresStaffRoleAndPersistsAssignmentHistory()
    {
        var claim = CreateClaim();
        var denied = new AssignClaimHandler(new InMemoryUnitOfWork(claim), User("claimant", ApplicationRole.Claimant, "team-a"), new FixedClock(Now));
        await Assert.ThrowsAsync<AccessDeniedException>(() => denied.HandleAsync(new AssignClaim(claim.Id, "v1", "team-a", "claimant")));

        var unit = new InMemoryUnitOfWork(claim);
        var handler = new AssignClaimHandler(unit, User("manager-a", ApplicationRole.Manager, "team-a"), new FixedClock(Now));
        await handler.HandleAsync(new AssignClaim(claim.Id, "v1", "team-a", "officer-a", "Initial assignment"));
        await handler.HandleAsync(new AssignClaim(claim.Id, "v1", "team-a", "officer-b", "Reassignment"));
        Assert.Equal("officer-b", claim.Assignment!.OfficerSubjectId);
        Assert.Equal(2, unit.WorkflowRecords.OfType<ClaimAssignmentHistory>().Count());
    }

    private static Claim CreateClaim() => new(Guid.NewGuid(), "CLM-TEST-1", "claimant-1",
        new ClaimantContactSnapshot("Demo Claimant", "claimant@example.test", null), "XXX", LineOfBusiness.Motor,
        new IncidentFacts(Now.AddDays(-1), "Synthetic test incident."), new MotorClaimDetails(), Now.AddHours(-1), Now);

    private static ClaimantClaimCard Card() => new(Guid.NewGuid(), "CLM-TEST-1", "XXX", LineOfBusiness.Motor,
        ClaimStatus.Submitted, Now, "v1");

    private static ClaimantClaimDetail SafeDetail() => new(Guid.NewGuid(), "CLM-TEST-1", "XXX", LineOfBusiness.Motor,
        ClaimStatus.Submitted, Now.AddHours(-1), Now, new ClaimantContactDto("Demo", "demo@example.test", null),
        new IncidentDto(Now.AddDays(-1), "Synthetic."), new MotorClaimDetailsDto(null, null, null, null, null, null, null), null, [], [], "v1");

    private static FakeUser User(string subject, ApplicationRole role, params string[] teams) => new(subject,
        new HashSet<ApplicationRole> { role }, teams.ToHashSet(StringComparer.Ordinal));

    private sealed class FixedClock(DateTimeOffset value) : IClock { public DateTimeOffset UtcNow => value; }
    private sealed class ClaimNumbers : IClaimNumberGenerator { public string NextClaimNumber(DateTimeOffset submittedAt) => "CLM-TEST-1"; }
    private sealed record FakeUser(string? SubjectId, IReadOnlySet<ApplicationRole> Roles, IReadOnlySet<string> TeamIds) : ICurrentUserContext
    { public IReadOnlySet<string> MarketCodes { get; } = new HashSet<string>(["SGP", "XXX"], StringComparer.OrdinalIgnoreCase); }

    private sealed class FakeQueryStore : IClaimQueryStore
    {
        public ClaimantClaimRecord? ClaimantClaim { get; init; }
        public Task<ClaimantClaimRecord?> FindClaimantClaimAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(ClaimantClaim);
        public Task<PageResult<ClaimantClaimRecord>> ListClaimantClaimsAsync(string subject, PageRequest page, CancellationToken cancellationToken = default) => Task.FromResult(new PageResult<ClaimantClaimRecord>([], null));
        public Task<PageResult<ClaimantHistoryRecord>> ListClaimantHistoryAsync(Guid id, PageRequest page, CancellationToken cancellationToken = default) => Task.FromResult(new PageResult<ClaimantHistoryRecord>([], null));
        public Task<StaffClaimRecord?> FindStaffClaimAsync(Guid id, IReadOnlySet<string> teams, CancellationToken cancellationToken = default) => Task.FromResult<StaffClaimRecord?>(null);
        public Task<PageResult<StaffQueueRecord>> ListStaffQueueAsync(WorkQueueFilter filter, IReadOnlySet<string> teams, PageRequest page, CancellationToken cancellationToken = default) => Task.FromResult(new PageResult<StaffQueueRecord>([], null));
        public Task<ManagerWorkloadSummary> GetManagerWorkloadAsync(IReadOnlySet<string> teams, IReadOnlySet<string> markets, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ManagerPerformanceSummary> GetManagerPerformanceAsync(IReadOnlySet<string> teams, IReadOnlySet<string> markets, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ManagerExposureSummary> GetManagerExposureAsync(IReadOnlySet<string> teams, IReadOnlySet<string> markets, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class InMemoryUnitOfWork : IClaimUnitOfWork
    {
        private readonly List<Claim> _claims = [];
        public InMemoryUnitOfWork(params Claim[] claims) => _claims.AddRange(claims);
        public IReadOnlyList<Claim> Claims => _claims;
        public List<ClaimActivity> Activities { get; } = [];
        public List<ClaimOutboxMessage> OutboxMessages { get; } = [];
        public List<ClaimWorkflowRecord> WorkflowRecords { get; } = [];
        public int CommitCount { get; private set; }
        public Task<IClaimWriteTransaction> BeginAsync(CancellationToken cancellationToken = default) => Task.FromResult<IClaimWriteTransaction>(new Transaction(this));

        private sealed class Transaction(InMemoryUnitOfWork owner) : IClaimWriteTransaction
        {
            private Claim? _newClaim;
            private readonly List<ClaimActivity> _activities = [];
            private readonly List<ClaimOutboxMessage> _events = [];
            private readonly List<ClaimWorkflowRecord> _workflow = [];
            public Task<Claim?> LoadClaimAsync(Guid claimId, string expectedVersion, CancellationToken cancellationToken = default)
            {
                if (expectedVersion != "v1") throw new ConcurrencyConflictException("Stale test version.");
                return Task.FromResult(owner._claims.SingleOrDefault(claim => claim.Id == claimId));
            }
            public Task AddClaimAsync(Claim claim, CancellationToken cancellationToken = default) { _newClaim = claim; return Task.CompletedTask; }
            public Task AddWorkflowRecordAsync(ClaimWorkflowRecord record, CancellationToken cancellationToken = default) { _workflow.Add(record); return Task.CompletedTask; }
            public Task AddActivityAsync(ClaimActivity activity, CancellationToken cancellationToken = default) { _activities.Add(activity); return Task.CompletedTask; }
            public Task AddOutboxMessageAsync(ClaimOutboxMessage message, CancellationToken cancellationToken = default) { _events.Add(message); return Task.CompletedTask; }
            public Task<string> CommitAsync(CancellationToken cancellationToken = default)
            {
                if (_newClaim is not null) owner._claims.Add(_newClaim);
                owner.Activities.AddRange(_activities); owner.OutboxMessages.AddRange(_events); owner.WorkflowRecords.AddRange(_workflow);
                owner.CommitCount++;
                return Task.FromResult("v2");
            }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
