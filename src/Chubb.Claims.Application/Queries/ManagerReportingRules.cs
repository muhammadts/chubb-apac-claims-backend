using Chubb.Claims.Domain.Claims;

namespace Chubb.Claims.Application.Queries;

/// <summary>Shared, deliberately small definitions for the manager read projections.</summary>
public static class ManagerReportingRules
{
    public static IReadOnlySet<ClaimStatus> CurrentStatuses { get; } = new HashSet<ClaimStatus>
    {
        ClaimStatus.Submitted, ClaimStatus.InReview, ClaimStatus.Assessment,
        ClaimStatus.AwaitingInformation, ClaimStatus.Approved
    };

    public static bool IsInWindow(DateTimeOffset occurredUtc, DateTimeOffset fromUtc, DateTimeOffset toUtc) =>
        occurredUtc >= fromUtc && occurredUtc < toUtc;

    public static string AgeBand(DateTimeOffset asOfUtc, DateTimeOffset submittedUtc)
    {
        var days = Math.Max(0, (int)(asOfUtc - submittedUtc).TotalDays);
        return days <= 7 ? "0-7 days" : days <= 30 ? "8-30 days" : days <= 90 ? "31-90 days" : "90+ days";
    }

    public static IReadOnlyList<ExposureGroup> GroupExposure(IEnumerable<(string Market, string? Currency, decimal? Amount)> reserves) =>
        reserves.Where(x => x.Amount.HasValue && !string.IsNullOrWhiteSpace(x.Currency))
            .GroupBy(x => (x.Market, Currency: x.Currency!))
            .Select(g => new ExposureGroup(g.Key.Market, g.Key.Currency, g.Sum(x => x.Amount!.Value), g.Count()))
            .ToArray();

    public static int CountMissingReserves(IEnumerable<(string Market, string? Currency, decimal? Amount)> reserves) =>
        reserves.Count(x => !x.Amount.HasValue || string.IsNullOrWhiteSpace(x.Currency));
}
