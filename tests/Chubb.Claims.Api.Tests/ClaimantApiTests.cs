using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Chubb.Claims.Application.Abstractions;
using Chubb.Claims.Application.Contracts;
using Chubb.Claims.Application.Queries;
using Chubb.Claims.Domain.Claims;
using Chubb.Claims.Infrastructure;
using Chubb.Claims.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using DomainClaim = Chubb.Claims.Domain.Claims.Claim;

namespace Chubb.Claims.Api.Tests;

public sealed class ClaimantApiTests
{
    [Fact]
    public async Task Submission_uses_authenticated_subject_and_returns_created()
    {
        var unit = new MemoryUnitOfWork();
        await using var factory = new TestApiFactory(unit, new MemoryQueryStore { Claim = ClaimRecord("claimant-a") });
        using var client = AuthenticatedClient(factory, "claimant-a");
        using var response = await client.PostAsync("/api/v1/claims", Json("""
            {"marketCode":"SGP","lineOfBusiness":"Motor","contact":{"name":"Demo Claimant","email":"claimant@example.test"},"incident":{"occurredAt":"2026-11-01T09:00:00+08:00","description":"Demo incident"},"motor":{"make":"Example","model":"One"}}
            """));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("claimant-a", unit.AddedClaim?.ClaimantSubjectId);
        Assert.Equal("Submitted", JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("status").GetString());
        Assert.Equal(LineOfBusiness.Motor, unit.AddedClaim?.LineOfBusiness);
        Assert.NotNull(response.Headers.ETag);
    }

    [Fact]
    public async Task Property_submission_accepts_property_details()
    {
        var unit = new MemoryUnitOfWork();
        await using var factory = new TestApiFactory(unit, new MemoryQueryStore());
        using var client = AuthenticatedClient(factory, "claimant-a");
        using var response = await client.PostAsync("/api/v1/claims", Json("""
            {"marketCode":"SGP","lineOfBusiness":"Property","contact":{"name":"Demo Claimant","email":"claimant@example.test"},"incident":{"occurredAt":"2026-11-01T09:00:00+08:00","description":"Demo incident"},"property":{"propertyType":"Apartment","city":"Singapore"}}
            """));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.IsType<PropertyClaimDetails>(unit.AddedClaim?.Details);
    }

    [Fact]
    public async Task Claimant_cannot_read_another_subjects_claim()
    {
        var query = new MemoryQueryStore { Claim = ClaimRecord("different-subject") };
        await using var factory = new TestApiFactory(new MemoryUnitOfWork(), query);
        using var client = AuthenticatedClient(factory, "claimant-a");

        using var response = await client.GetAsync($"/api/v1/claims/{ClaimId:D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(true, HttpStatusCode.Created)]
    [InlineData(false, HttpStatusCode.Conflict)]
    public async Task Information_response_requires_an_open_request(bool open, HttpStatusCode expected)
    {
        var unit = new MemoryUnitOfWork { LoadedClaim = OwnedClaim(open) };
        await using var factory = new TestApiFactory(unit, new MemoryQueryStore { Claim = ClaimRecord("claimant-a") });
        using var client = AuthenticatedClient(factory, "claimant-a");
        client.DefaultRequestHeaders.IfMatch.Add(new EntityTagHeaderValue("\"v1\""));

        using var response = await client.PostAsync($"/api/v1/claims/{ClaimId:D}/information-requests/{RequestId:D}/responses",
            Json("{\"responseText\":\"Additional detail\"}"));

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Claimant_detail_serialization_excludes_staff_only_fields()
    {
        var query = new MemoryQueryStore { Claim = ClaimRecord("claimant-a") };
        await using var factory = new TestApiFactory(new MemoryUnitOfWork(), query);
        using var client = AuthenticatedClient(factory, "claimant-a");

        using var response = await client.GetAsync($"/api/v1/claims/{ClaimId:D}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("internalNotes", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rationale", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("objectKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("activities", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Claimant_policy_returns_unauthorized_without_authentication()
    {
        await using var factory = new TestApiFactory(new MemoryUnitOfWork(), new MemoryQueryStore());
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/v1/claims");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Claimant_policy_forbids_an_authenticated_non_claimant()
    {
        await using var factory = new TestApiFactory(new MemoryUnitOfWork(), new MemoryQueryStore());
        using var client = AuthenticatedClient(factory, "officer-1");
        client.DefaultRequestHeaders.Remove("X-Test-Role");
        client.DefaultRequestHeaders.Add("X-Test-Role", "ClaimsOfficer");
        using var response = await client.GetAsync("/api/v1/claims");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpClient AuthenticatedClient(TestApiFactory factory, string subject)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Test-Role", "Claimant");
        return client;
    }
    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");
    private static readonly Guid ClaimId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid RequestId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset At = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static ClaimantClaimRecord ClaimRecord(string owner) => new(owner,
        new ClaimantClaimCard(ClaimId, "CLM-DEMO-1", "SGP", LineOfBusiness.Motor, ClaimStatus.AwaitingInformation, At, "v1"),
        new ClaimantClaimDetail(ClaimId, "CLM-DEMO-1", "SGP", LineOfBusiness.Motor, ClaimStatus.AwaitingInformation,
            At, At, new("Demo Claimant", "claimant@example.test", null), new(At, "Demo incident"),
            new MotorClaimDetailsDto(null, "Example", "One", null, null, null, null), null,
            [new(RequestId, "Please provide more information.", At, true, null)],
            [new(At, "InformationRequested", "Additional information was requested.")], "v1"));

    private static DomainClaim OwnedClaim(bool open) => DomainClaim.Rehydrate(ClaimId, "CLM-DEMO-1", "claimant-a",
        new ClaimantContactSnapshot("Demo Claimant", "claimant@example.test", null), "SGP", LineOfBusiness.Motor,
        new IncidentFacts(At, "Demo incident"), new MotorClaimDetails(), At, At, ClaimStatus.AwaitingInformation,
        null, [ClaimInformationRequest.Rehydrate(RequestId, "staff-1", "Please provide more information.", At,
            open ? null : new ClaimInformationResponse("claimant-a", At, "Already responded"))], [], null, null, null);

    private sealed class TestApiFactory(MemoryUnitOfWork unit, MemoryQueryStore query) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IClaimUnitOfWork>();
                services.AddSingleton<IClaimUnitOfWork>(unit);
                services.RemoveAll<IClaimQueryStore>();
                services.AddSingleton<IClaimQueryStore>(query);
                services.AddAuthentication(options => { options.DefaultAuthenticateScheme = "Test"; options.DefaultChallengeScheme = "Test"; })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }
    }

    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Subject", out var subject)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[] { new System.Security.Claims.Claim("sub", subject.ToString()), new System.Security.Claims.Claim(ClaimTypes.Role, Request.Headers["X-Test-Role"].ToString()) };
            var identity = new ClaimsIdentity(claims, "Test", ClaimTypes.Name, ClaimTypes.Role);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }

    private sealed class MemoryQueryStore : IClaimQueryStore
    {
        public ClaimantClaimRecord? Claim { get; init; }
        public Task<ClaimantClaimRecord?> FindClaimantClaimAsync(Guid id, CancellationToken token = default) => Task.FromResult(id == ClaimId ? Claim : null);
        public Task<PageResult<ClaimantClaimRecord>> ListClaimantClaimsAsync(string subject, PageRequest page, CancellationToken token = default) => Task.FromResult(new PageResult<ClaimantClaimRecord>([], null));
        public Task<PageResult<ClaimantHistoryRecord>> ListClaimantHistoryAsync(Guid id, PageRequest page, CancellationToken token = default) => Task.FromResult(new PageResult<ClaimantHistoryRecord>([], null));
        public Task<StaffClaimRecord?> FindStaffClaimAsync(Guid id, IReadOnlySet<string> teams, CancellationToken token = default) => Task.FromResult<StaffClaimRecord?>(null);
        public Task<PageResult<StaffQueueRecord>> ListStaffQueueAsync(WorkQueueFilter filter, IReadOnlySet<string> teams, PageRequest page, CancellationToken token = default) => Task.FromResult(new PageResult<StaffQueueRecord>([], null));
        public Task<ManagerWorkloadSummary> GetManagerWorkloadAsync(IReadOnlySet<string> teams, CancellationToken token = default) => throw new NotImplementedException();
        public Task<ManagerPerformanceSummary> GetManagerPerformanceAsync(IReadOnlySet<string> teams, DateTimeOffset from, DateTimeOffset to, CancellationToken token = default) => throw new NotImplementedException();
        public Task<ManagerExposureSummary> GetManagerExposureAsync(IReadOnlySet<string> teams, CancellationToken token = default) => throw new NotImplementedException();
    }

    private sealed class MemoryUnitOfWork : IClaimUnitOfWork
    {
        public DomainClaim? AddedClaim { get; private set; }
        public DomainClaim? LoadedClaim { get; init; }
        public Task<IClaimWriteTransaction> BeginAsync(CancellationToken token = default) => Task.FromResult<IClaimWriteTransaction>(new Transaction(this));
        private sealed class Transaction(MemoryUnitOfWork owner) : IClaimWriteTransaction
        {
            public Task<DomainClaim?> LoadClaimAsync(Guid id, string version, CancellationToken token = default) => Task.FromResult(id == ClaimId && version == "v1" ? owner.LoadedClaim : null);
            public Task AddClaimAsync(DomainClaim claim, CancellationToken token = default) { owner.AddedClaim = claim; return Task.CompletedTask; }
            public Task AddWorkflowRecordAsync(ClaimWorkflowRecord record, CancellationToken token = default) => Task.CompletedTask;
            public Task AddActivityAsync(ClaimActivity activity, CancellationToken token = default) => Task.CompletedTask;
            public Task AddOutboxMessageAsync(ClaimOutboxMessage message, CancellationToken token = default) => Task.CompletedTask;
            public Task<string> CommitAsync(CancellationToken token = default) => Task.FromResult("v2");
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
