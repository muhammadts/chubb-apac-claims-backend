using Chubb.Claims.Domain.Claims;

namespace Chubb.Claims.Domain.Tests;

public sealed class ClaimLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ApprovedClaimCanMoveThroughInformationRequestAssessmentAndSettlement()
    {
        var claim = CreateClaim();

        claim.StartReview("officer-1");
        var requestId = claim.RequestInformation("officer-1", "Please provide further details.", Now);
        Assert.Equal(ClaimStatus.AwaitingInformation, claim.Status);
        Assert.Throws<InvalidOperationException>(() => claim.ResumeReview("officer-1"));
        claim.SubmitInformationResponse(requestId, "claimant-1", "Additional facts", Now.AddHours(1));
        claim.ResumeReview("officer-1");
        claim.RecordAssessment(new ClaimAssessment("officer-1", Now.AddHours(2), "Initial", "Assessment recorded."));
        claim.RecordDecision(new ClaimDecision(DecisionType.Approved, "Decision recorded.", "officer-1", Now.AddHours(3)));
        claim.CompleteSettlement(new ClaimSettlement("officer-1", Now.AddDays(2)));

        Assert.Equal(ClaimStatus.Settled, claim.Status);
        Assert.Equal("claimant-1", claim.InformationRequests.Single().Response?.SubmittedBySubjectId);
        Assert.NotNull(claim.Settlement);
    }

    [Fact]
    public void LifecycleRejectsTransitionsThatAreNotAllowed()
    {
        var claim = CreateClaim();

        Assert.Throws<InvalidOperationException>(() => claim.RecordAssessment(
            new ClaimAssessment("officer-1", Now, "Initial", "Assessment recorded.")));
        Assert.Throws<InvalidOperationException>(() => claim.RecordDecision(
            new ClaimDecision(DecisionType.Approved, "Decision recorded.", "officer-1", Now)));
        Assert.Throws<InvalidOperationException>(() => claim.CompleteSettlement(
            new ClaimSettlement("officer-1", Now)));
        Assert.Throws<InvalidOperationException>(() => claim.ResumeReview("officer-1"));
    }

    [Fact]
    public void DecisionIsFinalAndRejectedClaimCannotBeSettled()
    {
        var claim = CreateClaim();
        claim.StartReview("officer-1");
        claim.RecordAssessment(new ClaimAssessment("officer-1", Now, "Initial", "Assessment recorded."));
        var decision = new ClaimDecision(DecisionType.Rejected, "Decision recorded.", "officer-1", Now.AddMinutes(1));
        claim.RecordDecision(decision);

        Assert.Equal(ClaimStatus.Rejected, claim.Status);
        Assert.Same(decision, claim.Decision);
        Assert.Throws<InvalidOperationException>(() => claim.RecordDecision(
            new ClaimDecision(DecisionType.Approved, "Second decision.", "officer-2", Now.AddMinutes(2))));
        Assert.Throws<InvalidOperationException>(() => claim.CompleteSettlement(
            new ClaimSettlement("officer-1", Now.AddMinutes(3))));
        Assert.Equal(ClaimStatus.Rejected, claim.Status);
    }

    [Fact]
    public void OnlyOwningClaimantCanRespondToOpenRequest()
    {
        var claim = CreateClaim();
        claim.StartReview("officer-1");
        var requestId = claim.RequestInformation("officer-1", "Please clarify.", Now);

        Assert.Throws<InvalidOperationException>(() => claim.SubmitInformationResponse(
            requestId, "another-claimant", "Response", Now.AddMinutes(1)));

        claim.SubmitInformationResponse(requestId, "claimant-1", "Response", Now.AddMinutes(2));
        Assert.Throws<InvalidOperationException>(() => claim.SubmitInformationResponse(
            requestId, "claimant-1", "Duplicate response", Now.AddMinutes(3)));
    }

    [Fact]
    public void ReserveEstimateRequiresNonNegativeDecimal19_4AmountAndCurrency()
    {
        var estimate = new MoneyEstimate(123.4500m, "USD");
        var claim = CreateClaim();
        claim.UpdateReserveEstimate(estimate);

        Assert.Same(estimate, claim.ReserveEstimate);
        Assert.Equal(123.4500m, claim.ReserveEstimate?.Amount);
        Assert.Equal("USD", claim.ReserveEstimate?.Currency);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoneyEstimate(-0.01m, "USD"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoneyEstimate(1.00001m, "USD"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoneyEstimate(1_000_000_000_000_000m, "USD"));
        Assert.Throws<ArgumentException>(() => new MoneyEstimate(1m, "usd"));
    }

    private static Claim CreateClaim() => new(
        Guid.NewGuid(),
        "CLM-2026-0001",
        "claimant-1",
        new ClaimantContactSnapshot("Sample Claimant", "claimant@example.test", null),
        "XXX",
        LineOfBusiness.Motor,
        new IncidentFacts(Now.AddDays(-1), "Demo incident facts."),
        new MotorClaimDetails(Make: "Demo make", Model: "Demo model"),
        Now.AddHours(-1),
        Now);
}
