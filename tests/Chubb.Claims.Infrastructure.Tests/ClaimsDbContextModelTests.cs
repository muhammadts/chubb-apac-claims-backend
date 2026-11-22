using Chubb.Claims.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Chubb.Claims.Infrastructure.Tests;

public sealed class ClaimsDbContextModelTests
{
    [Fact]
    public void Model_contains_exactly_the_twenty_assessment_tables()
    {
        var options = new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(string.Empty).Options;
        using var context = new ClaimsDbContext(options);
        var tables = context.Model.GetEntityTypes().Select(x => x.GetTableName()).OfType<string>().Distinct().Order().ToArray();

        Assert.Equal(20, tables.Length);
        Assert.Equal(new[]
        {
            "ClaimActivities", "ClaimAssessments", "ClaimAssignments", "ClaimDecisions", "ClaimDocuments", "ClaimInformationRequests",
            "ClaimInformationResponses", "ClaimNotes", "ClaimReserveHistory", "ClaimSettlements", "ClaimantProfiles", "Claims",
            "Markets", "MotorClaimDetails", "OutboxMessages", "PropertyClaimDetails", "StaffProfiles", "TeamMarkets", "TeamMemberships", "Teams"
        }.OrderBy(x => x, StringComparer.Ordinal), tables.OrderBy(x => x, StringComparer.Ordinal));
        Assert.DoesNotContain(tables, x => x.StartsWith("Policy", StringComparison.Ordinal));
        Assert.DoesNotContain("IdempotencyRecords", tables);
    }

    [Fact]
    public void Claim_mapping_uses_sql_server_rowversion_and_decimal_19_4()
    {
        var options = new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(string.Empty).Options;
        using var context = new ClaimsDbContext(options);
        var claim = context.Model.FindEntityType("Chubb.Claims.Infrastructure.Persistence.ClaimRow")!;
        Assert.Equal("rowversion", claim.FindProperty("RowVersion")?.GetColumnType());
        Assert.Equal("decimal(19,4)", claim.FindProperty("CurrentReserveAmount")?.GetColumnType());
    }

    [Fact]
    public void Claim_assignments_allow_only_one_active_assignment_per_claim()
    {
        var options = new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(string.Empty).Options;
        using var context = new ClaimsDbContext(options);
        var assignment = context.Model.FindEntityType("Chubb.Claims.Infrastructure.Persistence.ClaimAssignmentRow")!;
        var index = Assert.Single(assignment.GetIndexes(), i => i.IsUnique && i.Properties.Any(p => p.Name == "ClaimId"));
        Assert.Equal("[UnassignedUtc] IS NULL", index.GetFilter());
    }
}
