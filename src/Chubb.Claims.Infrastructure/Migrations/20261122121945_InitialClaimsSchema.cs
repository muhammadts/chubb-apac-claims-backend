using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chubb.Claims.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialClaimsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "ClaimantProfiles",
                schema: "dbo",
                columns: table => new
                {
                    SubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimantProfiles", x => x.SubjectId);
                });

            migrationBuilder.CreateTable(
                name: "Markets",
                schema: "dbo",
                columns: table => new
                {
                    MarketCode = table.Column<string>(type: "char(3)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultCurrency = table.Column<string>(type: "char(3)", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Markets", x => x.MarketCode);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "dbo",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    EventVersion = table.Column<short>(type: "smallint", nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LockId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.EventId);
                    table.CheckConstraint("CK_OutboxMessages_AttemptCount", "[AttemptCount] >= 0");
                    table.CheckConstraint("CK_OutboxMessages_EventVersion", "[EventVersion] > 0");
                });

            migrationBuilder.CreateTable(
                name: "StaffProfiles",
                schema: "dbo",
                columns: table => new
                {
                    SubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffProfiles", x => x.SubjectId);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                schema: "dbo",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.TeamId);
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                schema: "dbo",
                columns: table => new
                {
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ClaimantSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClaimantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClaimantEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ClaimantPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MarketCode = table.Column<string>(type: "char(3)", nullable: false),
                    LineOfBusiness = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PolicyNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IncidentOccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReportedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CurrentReserveAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    CurrentReserveCurrency = table.Column<string>(type: "char(3)", nullable: true),
                    SubmittedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClosedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.ClaimId);
                    table.CheckConstraint("CK_Claims_LineOfBusiness", "[LineOfBusiness] IN (N'Motor', N'Property')");
                    table.CheckConstraint("CK_Claims_ReservePair", "([CurrentReserveAmount] IS NULL AND [CurrentReserveCurrency] IS NULL) OR ([CurrentReserveAmount] IS NOT NULL AND [CurrentReserveCurrency] IS NOT NULL AND [CurrentReserveAmount] >= 0)");
                    table.CheckConstraint("CK_Claims_Status", "[Status] IN (N'Submitted', N'InReview', N'Assessment', N'AwaitingInformation', N'Approved', N'Rejected', N'Settled')");
                    table.ForeignKey(
                        name: "FK_Claims_ClaimantProfiles_ClaimantSubjectId",
                        column: x => x.ClaimantSubjectId,
                        principalSchema: "dbo",
                        principalTable: "ClaimantProfiles",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Claims_Markets_MarketCode",
                        column: x => x.MarketCode,
                        principalSchema: "dbo",
                        principalTable: "Markets",
                        principalColumn: "MarketCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamMarkets",
                schema: "dbo",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketCode = table.Column<string>(type: "char(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMarkets", x => new { x.TeamId, x.MarketCode });
                    table.ForeignKey(
                        name: "FK_TeamMarkets_Markets_MarketCode",
                        column: x => x.MarketCode,
                        principalSchema: "dbo",
                        principalTable: "Markets",
                        principalColumn: "MarketCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMarkets_Teams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "dbo",
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamMemberships",
                schema: "dbo",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AddedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMemberships", x => new { x.TeamId, x.StaffSubjectId, x.EffectiveFromUtc });
                    table.ForeignKey(
                        name: "FK_TeamMemberships_StaffProfiles_StaffSubjectId",
                        column: x => x.StaffSubjectId,
                        principalSchema: "dbo",
                        principalTable: "StaffProfiles",
                        principalColumn: "SubjectId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMemberships_Teams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "dbo",
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimActivities",
                schema: "dbo",
                columns: table => new
                {
                    ActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ActorKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Visibility = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OccurredUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimActivities", x => x.ActivityId);
                    table.CheckConstraint("CK_ClaimActivities_Visibility", "[Visibility] IN (N'Claimant', N'Staff', N'System')");
                    table.ForeignKey(
                        name: "FK_ClaimActivities_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "dbo",
                        principalTable: "Claims",
                        principalColumn: "ClaimId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimAssessments",
                schema: "dbo",
                columns: table => new
                {
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AssessmentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Recommendation = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Rationale = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimAssessments", x => x.AssessmentId);
                    table.ForeignKey(
                        name: "FK_ClaimAssessments_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "dbo",
                        principalTable: "Claims",
                        principalColumn: "ClaimId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimAssignments",
                schema: "dbo",
                columns: table => new
                {
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfficerSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AssignedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AssignedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UnassignedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimAssignments", x => x.AssignmentId);
                    table.ForeignKey(
                        name: "FK_ClaimAssignments_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "dbo",
                        principalTable: "Claims",
                        principalColumn: "ClaimId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimAssignments_Teams_TeamId",
                        column: x => x.TeamId,
                        principalSchema: "dbo",
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimDecisions",
                schema: "dbo",
                columns: table => new
                {
                    DecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DecisionType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DecisionAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    DecisionCurrency = table.Column<string>(type: "char(3)", nullable: true),
                    DecidedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DecidedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimDecisions", x => x.DecisionId);
                    table.CheckConstraint("CK_ClaimDecisions_AmountPair", "([DecisionAmount] IS NULL AND [DecisionCurrency] IS NULL) OR ([DecisionAmount] IS NOT NULL AND [DecisionCurrency] IS NOT NULL AND [DecisionAmount] >= 0)");
                    table.CheckConstraint("CK_ClaimDecisions_Type", "[DecisionType] IN (N'Approved', N'Rejected')");
                    table.ForeignKey(
                        name: "FK_ClaimDecisions_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "dbo",
                        principalTable: "Claims",
                        principalColumn: "ClaimId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimInformationRequests",
                schema: "dbo",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Question = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequestedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DueUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResolvedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimInformationRequests", x => x.RequestId);
                    table.CheckConstraint("CK_ClaimInformationRequests_Status", "[Status] IN (N'Open', N'Responded', N'Cancelled')");
                    table.ForeignKey(
                        name: "FK_ClaimInformationRequests_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "dbo",
                        principalTable: "Claims",
                        principalColumn: "ClaimId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimNotes",
                schema: "dbo",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NoteText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EditedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimNotes", x => x.NoteId);
                    table.ForeignKey(
                        name: "FK_ClaimNotes_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "dbo",
                        principalTable: "Claims",
                        principalColumn: "ClaimId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimReserveHistory",
                schema: "dbo",
                columns: table => new
                {
                    ReserveEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "char(3)", nullable: false),
                    EstimateType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimReserveHistory", x => x.ReserveEntryId);
                    table.CheckConstraint("CK_ClaimReserveHistory_Amount", "[Amount] >= 0");
                    table.ForeignKey(
                        name: "FK_ClaimReserveHistory_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "dbo",
                        principalTable: "Claims",
                        principalColumn: "ClaimId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimSettlements",
                schema: "dbo",
                columns: table => new
                {
                    SettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Currency = table.Column<string>(type: "char(3)", nullable: true),
                    RecordedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimSettlements", x => x.SettlementId);
                    table.CheckConstraint("CK_ClaimSettlements_AmountPair", "([Amount] IS NULL AND [Currency] IS NULL) OR ([Amount] IS NOT NULL AND [Currency] IS NOT NULL AND [Amount] >= 0)");
                    table.CheckConstraint("CK_ClaimSettlements_Status", "[Status] IN (N'Pending', N'Completed')");
                    table.ForeignKey(
                        name: "FK_ClaimSettlements_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "dbo",
                        principalTable: "Claims",
                        principalColumn: "ClaimId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MotorClaimDetails",
                schema: "dbo",
                columns: table => new
                {
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleRegistration = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Make = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModelYear = table.Column<short>(type: "smallint", nullable: true),
                    VehicleUse = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Drivable = table.Column<bool>(type: "bit", nullable: true),
                    OtherPartyInvolved = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotorClaimDetails", x => x.ClaimId);
                    table.ForeignKey(
                        name: "FK_MotorClaimDetails_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "dbo",
                        principalTable: "Claims",
                        principalColumn: "ClaimId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PropertyClaimDetails",
                schema: "dbo",
                columns: table => new
                {
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CountryCode = table.Column<string>(type: "char(2)", nullable: true),
                    LossCause = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AffectedAreaDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Habitable = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyClaimDetails", x => x.ClaimId);
                    table.ForeignKey(
                        name: "FK_PropertyClaimDetails_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "dbo",
                        principalTable: "Claims",
                        principalColumn: "ClaimId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimInformationResponses",
                schema: "dbo",
                columns: table => new
                {
                    ResponseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResponseText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SubmittedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimInformationResponses", x => x.ResponseId);
                    table.ForeignKey(
                        name: "FK_ClaimInformationResponses_ClaimInformationRequests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "dbo",
                        principalTable: "ClaimInformationRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimDocuments",
                schema: "dbo",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResponseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Audience = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StorageProvider = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ObjectKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LengthBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "char(64)", nullable: false),
                    UploadStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UploadedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeletedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimDocuments", x => x.DocumentId);
                    table.CheckConstraint("CK_ClaimDocuments_Audience", "[Audience] IN (N'Claimant', N'Staff')");
                    table.CheckConstraint("CK_ClaimDocuments_LengthBytes", "[LengthBytes] >= 0");
                    table.ForeignKey(
                        name: "FK_ClaimDocuments_ClaimInformationResponses_ResponseId",
                        column: x => x.ResponseId,
                        principalSchema: "dbo",
                        principalTable: "ClaimInformationResponses",
                        principalColumn: "ResponseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClaimDocuments_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalSchema: "dbo",
                        principalTable: "Claims",
                        principalColumn: "ClaimId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimActivities_ActivityType_OccurredUtc",
                schema: "dbo",
                table: "ClaimActivities",
                columns: new[] { "ActivityType", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimActivities_ClaimId_OccurredUtc",
                schema: "dbo",
                table: "ClaimActivities",
                columns: new[] { "ClaimId", "OccurredUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAssessments_ClaimId_CreatedUtc",
                schema: "dbo",
                table: "ClaimAssessments",
                columns: new[] { "ClaimId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAssignments_ClaimId",
                schema: "dbo",
                table: "ClaimAssignments",
                column: "ClaimId",
                unique: true,
                filter: "[UnassignedUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAssignments_OfficerSubjectId_UnassignedUtc_AssignedUtc",
                schema: "dbo",
                table: "ClaimAssignments",
                columns: new[] { "OfficerSubjectId", "UnassignedUtc", "AssignedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAssignments_TeamId_UnassignedUtc_AssignedUtc",
                schema: "dbo",
                table: "ClaimAssignments",
                columns: new[] { "TeamId", "UnassignedUtc", "AssignedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimDecisions_ClaimId",
                schema: "dbo",
                table: "ClaimDecisions",
                column: "ClaimId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimDocuments_ClaimId_UploadedUtc",
                schema: "dbo",
                table: "ClaimDocuments",
                columns: new[] { "ClaimId", "UploadedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimDocuments_ResponseId",
                schema: "dbo",
                table: "ClaimDocuments",
                column: "ResponseId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimInformationRequests_ClaimId_Status_RequestedUtc",
                schema: "dbo",
                table: "ClaimInformationRequests",
                columns: new[] { "ClaimId", "Status", "RequestedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimInformationResponses_RequestId",
                schema: "dbo",
                table: "ClaimInformationResponses",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimNotes_ClaimId",
                schema: "dbo",
                table: "ClaimNotes",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimReserveHistory_ClaimId_RecordedUtc",
                schema: "dbo",
                table: "ClaimReserveHistory",
                columns: new[] { "ClaimId", "RecordedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_ClaimantSubjectId_CreatedUtc",
                schema: "dbo",
                table: "Claims",
                columns: new[] { "ClaimantSubjectId", "CreatedUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_ClaimNumber",
                schema: "dbo",
                table: "Claims",
                column: "ClaimNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Claims_IncidentOccurredUtc_MarketCode_Status",
                schema: "dbo",
                table: "Claims",
                columns: new[] { "IncidentOccurredUtc", "MarketCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_MarketCode_Status_SubmittedUtc",
                schema: "dbo",
                table: "Claims",
                columns: new[] { "MarketCode", "Status", "SubmittedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_Status_UpdatedUtc",
                schema: "dbo",
                table: "Claims",
                columns: new[] { "Status", "UpdatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_SubmittedUtc_Status_MarketCode",
                schema: "dbo",
                table: "Claims",
                columns: new[] { "SubmittedUtc", "Status", "MarketCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimSettlements_ClaimId",
                schema: "dbo",
                table: "ClaimSettlements",
                column: "ClaimId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_PublishedUtc_NextAttemptUtc_CreatedUtc",
                schema: "dbo",
                table: "OutboxMessages",
                columns: new[] { "PublishedUtc", "NextAttemptUtc", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMarkets_MarketCode",
                schema: "dbo",
                table: "TeamMarkets",
                column: "MarketCode");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberships_StaffSubjectId_EffectiveToUtc_TeamId",
                schema: "dbo",
                table: "TeamMemberships",
                columns: new[] { "StaffSubjectId", "EffectiveToUtc", "TeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_TeamCode",
                schema: "dbo",
                table: "Teams",
                column: "TeamCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimActivities",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ClaimAssessments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ClaimAssignments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ClaimDecisions",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ClaimDocuments",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ClaimNotes",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ClaimReserveHistory",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ClaimSettlements",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "MotorClaimDetails",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PropertyClaimDetails",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TeamMarkets",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TeamMemberships",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ClaimInformationResponses",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "StaffProfiles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Teams",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ClaimInformationRequests",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Claims",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ClaimantProfiles",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Markets",
                schema: "dbo");
        }
    }
}
