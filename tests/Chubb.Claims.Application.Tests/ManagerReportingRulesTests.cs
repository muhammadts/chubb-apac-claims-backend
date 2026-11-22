using Chubb.Claims.Application.Queries;
using Chubb.Claims.Domain.Claims;

namespace Chubb.Claims.Application.Tests;

public sealed class ManagerReportingRulesTests
{
    [Fact]
    public void Current_statuses_include_open_and_approved_but_exclude_closed()
    {
        Assert.Equal(new[] { ClaimStatus.Submitted, ClaimStatus.InReview, ClaimStatus.Assessment,
            ClaimStatus.AwaitingInformation, ClaimStatus.Approved }.ToHashSet(), ManagerReportingRules.CurrentStatuses);
        Assert.DoesNotContain(ClaimStatus.Rejected, ManagerReportingRules.CurrentStatuses);
        Assert.DoesNotContain(ClaimStatus.Settled, ManagerReportingRules.CurrentStatuses);
    }

    [Fact]
    public void Exposure_keeps_market_currency_pairs_separate_and_counts_missing_reserves()
    {
        var reserves = new[]
        {
            ("SGP", (string?)"SGD", (decimal?)10m), ("SGP", (string?)"SGD", (decimal?)5m),
            ("SGP", (string?)"USD", (decimal?)20m), ("AUS", (string?)"SGD", (decimal?)7m),
            ("SGP", (string?)null, (decimal?)null)
        };
        var grouped = ManagerReportingRules.GroupExposure(reserves);
        Assert.Equal(1, ManagerReportingRules.CountMissingReserves(reserves));
        Assert.Equal(3, grouped.Count);
        Assert.Equal(15m, grouped.Single(x => x.MarketCode == "SGP" && x.Currency == "SGD").Amount);
        Assert.Equal(20m, grouped.Single(x => x.MarketCode == "SGP" && x.Currency == "USD").Amount);
        Assert.DoesNotContain(grouped, x => x.MarketCode == "SGP" && x.Currency == "SGD" && x.Amount == 42m);
    }

    [Fact]
    public void Performance_window_is_start_inclusive_and_end_exclusive()
    {
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);
        Assert.True(ManagerReportingRules.IsInWindow(from, from, to));
        Assert.True(ManagerReportingRules.IsInWindow(to.AddTicks(-1), from, to));
        Assert.False(ManagerReportingRules.IsInWindow(to, from, to));
        Assert.False(ManagerReportingRules.IsInWindow(from.AddTicks(-1), from, to));
    }

    [Theory]
    [InlineData(0, "0-7 days")]
    [InlineData(7, "0-7 days")]
    [InlineData(8, "8-30 days")]
    [InlineData(30, "8-30 days")]
    [InlineData(31, "31-90 days")]
    [InlineData(90, "31-90 days")]
    [InlineData(91, "90+ days")]
    public void Ageing_bands_use_elapsed_utc_days(int days, string expected)
    {
        var asOf = DateTimeOffset.UtcNow;
        Assert.Equal(expected, ManagerReportingRules.AgeBand(asOf, asOf.AddDays(-days)));
    }
}
