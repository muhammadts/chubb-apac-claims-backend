IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[ClaimantProfiles] (
        [SubjectId] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Email] nvarchar(320) NULL,
        [Phone] nvarchar(50) NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [UpdatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ClaimantProfiles] PRIMARY KEY ([SubjectId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[Markets] (
        [MarketCode] char(3) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [DefaultCurrency] char(3) NOT NULL,
        [TimeZoneId] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Markets] PRIMARY KEY ([MarketCode])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[OutboxMessages] (
        [EventId] uniqueidentifier NOT NULL,
        [AggregateType] nvarchar(100) NOT NULL,
        [AggregateId] uniqueidentifier NOT NULL,
        [EventType] nvarchar(150) NOT NULL,
        [EventVersion] smallint NOT NULL,
        [OccurredUtc] datetimeoffset NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [PublishedUtc] datetimeoffset NULL,
        [AttemptCount] int NOT NULL,
        [NextAttemptUtc] datetimeoffset NOT NULL,
        [LastError] nvarchar(2000) NULL,
        [LockId] uniqueidentifier NULL,
        [LockedUntilUtc] datetimeoffset NULL,
        CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([EventId]),
        CONSTRAINT [CK_OutboxMessages_AttemptCount] CHECK ([AttemptCount] >= 0),
        CONSTRAINT [CK_OutboxMessages_EventVersion] CHECK ([EventVersion] > 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[StaffProfiles] (
        [SubjectId] nvarchar(200) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [UpdatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_StaffProfiles] PRIMARY KEY ([SubjectId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[Teams] (
        [TeamId] uniqueidentifier NOT NULL,
        [TeamCode] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Teams] PRIMARY KEY ([TeamId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[Claims] (
        [ClaimId] uniqueidentifier NOT NULL,
        [ClaimNumber] nvarchar(32) NOT NULL,
        [ClaimantSubjectId] nvarchar(200) NOT NULL,
        [ClaimantName] nvarchar(200) NOT NULL,
        [ClaimantEmail] nvarchar(320) NOT NULL,
        [ClaimantPhone] nvarchar(50) NULL,
        [MarketCode] char(3) NOT NULL,
        [LineOfBusiness] nvarchar(20) NOT NULL,
        [PolicyNumber] nvarchar(100) NULL,
        [IncidentOccurredUtc] datetimeoffset NOT NULL,
        [ReportedUtc] datetimeoffset NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [CurrentReserveAmount] decimal(19,4) NULL,
        [CurrentReserveCurrency] char(3) NULL,
        [SubmittedUtc] datetimeoffset NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [UpdatedUtc] datetimeoffset NOT NULL,
        [ClosedUtc] datetimeoffset NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Claims] PRIMARY KEY ([ClaimId]),
        CONSTRAINT [CK_Claims_LineOfBusiness] CHECK ([LineOfBusiness] IN (N'Motor', N'Property')),
        CONSTRAINT [CK_Claims_ReservePair] CHECK (([CurrentReserveAmount] IS NULL AND [CurrentReserveCurrency] IS NULL) OR ([CurrentReserveAmount] IS NOT NULL AND [CurrentReserveCurrency] IS NOT NULL AND [CurrentReserveAmount] >= 0)),
        CONSTRAINT [CK_Claims_Status] CHECK ([Status] IN (N'Submitted', N'InReview', N'Assessment', N'AwaitingInformation', N'Approved', N'Rejected', N'Settled')),
        CONSTRAINT [FK_Claims_ClaimantProfiles_ClaimantSubjectId] FOREIGN KEY ([ClaimantSubjectId]) REFERENCES [dbo].[ClaimantProfiles] ([SubjectId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Claims_Markets_MarketCode] FOREIGN KEY ([MarketCode]) REFERENCES [dbo].[Markets] ([MarketCode]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[TeamMarkets] (
        [TeamId] uniqueidentifier NOT NULL,
        [MarketCode] char(3) NOT NULL,
        CONSTRAINT [PK_TeamMarkets] PRIMARY KEY ([TeamId], [MarketCode]),
        CONSTRAINT [FK_TeamMarkets_Markets_MarketCode] FOREIGN KEY ([MarketCode]) REFERENCES [dbo].[Markets] ([MarketCode]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamMarkets_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [dbo].[Teams] ([TeamId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[TeamMemberships] (
        [TeamId] uniqueidentifier NOT NULL,
        [StaffSubjectId] nvarchar(200) NOT NULL,
        [EffectiveFromUtc] datetimeoffset NOT NULL,
        [EffectiveToUtc] datetimeoffset NULL,
        [AddedBySubjectId] nvarchar(200) NULL,
        CONSTRAINT [PK_TeamMemberships] PRIMARY KEY ([TeamId], [StaffSubjectId], [EffectiveFromUtc]),
        CONSTRAINT [FK_TeamMemberships_StaffProfiles_StaffSubjectId] FOREIGN KEY ([StaffSubjectId]) REFERENCES [dbo].[StaffProfiles] ([SubjectId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TeamMemberships_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [dbo].[Teams] ([TeamId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[ClaimActivities] (
        [ActivityId] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [ActivityType] nvarchar(80) NOT NULL,
        [ActorSubjectId] nvarchar(200) NULL,
        [ActorKind] nvarchar(20) NOT NULL,
        [Visibility] nvarchar(20) NOT NULL,
        [Summary] nvarchar(1000) NOT NULL,
        [OccurredUtc] datetimeoffset NOT NULL,
        [CorrelationId] nvarchar(100) NULL,
        CONSTRAINT [PK_ClaimActivities] PRIMARY KEY ([ActivityId]),
        CONSTRAINT [CK_ClaimActivities_Visibility] CHECK ([Visibility] IN (N'Claimant', N'Staff', N'System')),
        CONSTRAINT [FK_ClaimActivities_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [dbo].[Claims] ([ClaimId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[ClaimAssessments] (
        [AssessmentId] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [AssessorSubjectId] nvarchar(200) NOT NULL,
        [AssessmentType] nvarchar(40) NOT NULL,
        [Recommendation] nvarchar(40) NULL,
        [Rationale] nvarchar(4000) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ClaimAssessments] PRIMARY KEY ([AssessmentId]),
        CONSTRAINT [FK_ClaimAssessments_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [dbo].[Claims] ([ClaimId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[ClaimAssignments] (
        [AssignmentId] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [TeamId] uniqueidentifier NOT NULL,
        [OfficerSubjectId] nvarchar(200) NULL,
        [AssignedBySubjectId] nvarchar(200) NOT NULL,
        [AssignedUtc] datetimeoffset NOT NULL,
        [UnassignedUtc] datetimeoffset NULL,
        [Reason] nvarchar(500) NULL,
        CONSTRAINT [PK_ClaimAssignments] PRIMARY KEY ([AssignmentId]),
        CONSTRAINT [FK_ClaimAssignments_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [dbo].[Claims] ([ClaimId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ClaimAssignments_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [dbo].[Teams] ([TeamId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[ClaimDecisions] (
        [DecisionId] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [DecisionType] nvarchar(20) NOT NULL,
        [Reason] nvarchar(4000) NOT NULL,
        [DecisionAmount] decimal(19,4) NULL,
        [DecisionCurrency] char(3) NULL,
        [DecidedBySubjectId] nvarchar(200) NOT NULL,
        [DecidedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ClaimDecisions] PRIMARY KEY ([DecisionId]),
        CONSTRAINT [CK_ClaimDecisions_AmountPair] CHECK (([DecisionAmount] IS NULL AND [DecisionCurrency] IS NULL) OR ([DecisionAmount] IS NOT NULL AND [DecisionCurrency] IS NOT NULL AND [DecisionAmount] >= 0)),
        CONSTRAINT [CK_ClaimDecisions_Type] CHECK ([DecisionType] IN (N'Approved', N'Rejected')),
        CONSTRAINT [FK_ClaimDecisions_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [dbo].[Claims] ([ClaimId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[ClaimInformationRequests] (
        [RequestId] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [RequestedBySubjectId] nvarchar(200) NOT NULL,
        [Question] nvarchar(2000) NOT NULL,
        [RequestedUtc] datetimeoffset NOT NULL,
        [DueUtc] datetimeoffset NULL,
        [Status] nvarchar(20) NOT NULL,
        [ResolvedUtc] datetimeoffset NULL,
        CONSTRAINT [PK_ClaimInformationRequests] PRIMARY KEY ([RequestId]),
        CONSTRAINT [CK_ClaimInformationRequests_Status] CHECK ([Status] IN (N'Open', N'Responded', N'Cancelled')),
        CONSTRAINT [FK_ClaimInformationRequests_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [dbo].[Claims] ([ClaimId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[ClaimNotes] (
        [NoteId] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [AuthorSubjectId] nvarchar(200) NOT NULL,
        [NoteText] nvarchar(4000) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [EditedUtc] datetimeoffset NULL,
        [DeletedUtc] datetimeoffset NULL,
        CONSTRAINT [PK_ClaimNotes] PRIMARY KEY ([NoteId]),
        CONSTRAINT [FK_ClaimNotes_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [dbo].[Claims] ([ClaimId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[ClaimReserveHistory] (
        [ReserveEntryId] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [Amount] decimal(19,4) NOT NULL,
        [Currency] char(3) NOT NULL,
        [EstimateType] nvarchar(40) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [RecordedBySubjectId] nvarchar(200) NOT NULL,
        [RecordedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ClaimReserveHistory] PRIMARY KEY ([ReserveEntryId]),
        CONSTRAINT [CK_ClaimReserveHistory_Amount] CHECK ([Amount] >= 0),
        CONSTRAINT [FK_ClaimReserveHistory_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [dbo].[Claims] ([ClaimId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[ClaimSettlements] (
        [SettlementId] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Amount] decimal(19,4) NULL,
        [Currency] char(3) NULL,
        [RecordedBySubjectId] nvarchar(200) NOT NULL,
        [CreatedUtc] datetimeoffset NOT NULL,
        [CompletedUtc] datetimeoffset NULL,
        [Reference] nvarchar(100) NULL,
        CONSTRAINT [PK_ClaimSettlements] PRIMARY KEY ([SettlementId]),
        CONSTRAINT [CK_ClaimSettlements_AmountPair] CHECK (([Amount] IS NULL AND [Currency] IS NULL) OR ([Amount] IS NOT NULL AND [Currency] IS NOT NULL AND [Amount] >= 0)),
        CONSTRAINT [CK_ClaimSettlements_Status] CHECK ([Status] IN (N'Pending', N'Completed')),
        CONSTRAINT [FK_ClaimSettlements_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [dbo].[Claims] ([ClaimId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[MotorClaimDetails] (
        [ClaimId] uniqueidentifier NOT NULL,
        [VehicleRegistration] nvarchar(40) NULL,
        [Make] nvarchar(100) NULL,
        [Model] nvarchar(100) NULL,
        [ModelYear] smallint NULL,
        [VehicleUse] nvarchar(80) NULL,
        [Drivable] bit NULL,
        [OtherPartyInvolved] bit NULL,
        CONSTRAINT [PK_MotorClaimDetails] PRIMARY KEY ([ClaimId]),
        CONSTRAINT [FK_MotorClaimDetails_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [dbo].[Claims] ([ClaimId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[PropertyClaimDetails] (
        [ClaimId] uniqueidentifier NOT NULL,
        [PropertyType] nvarchar(80) NULL,
        [AddressLine1] nvarchar(200) NULL,
        [AddressLine2] nvarchar(200) NULL,
        [City] nvarchar(100) NULL,
        [Region] nvarchar(100) NULL,
        [PostalCode] nvarchar(30) NULL,
        [CountryCode] char(2) NULL,
        [LossCause] nvarchar(120) NULL,
        [AffectedAreaDescription] nvarchar(2000) NULL,
        [Habitable] bit NULL,
        CONSTRAINT [PK_PropertyClaimDetails] PRIMARY KEY ([ClaimId]),
        CONSTRAINT [FK_PropertyClaimDetails_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [dbo].[Claims] ([ClaimId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[ClaimInformationResponses] (
        [ResponseId] uniqueidentifier NOT NULL,
        [RequestId] uniqueidentifier NOT NULL,
        [SubmittedBySubjectId] nvarchar(200) NOT NULL,
        [ResponseText] nvarchar(4000) NULL,
        [SubmittedUtc] datetimeoffset NOT NULL,
        CONSTRAINT [PK_ClaimInformationResponses] PRIMARY KEY ([ResponseId]),
        CONSTRAINT [FK_ClaimInformationResponses_ClaimInformationRequests_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [dbo].[ClaimInformationRequests] ([RequestId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE TABLE [dbo].[ClaimDocuments] (
        [DocumentId] uniqueidentifier NOT NULL,
        [ClaimId] uniqueidentifier NOT NULL,
        [ResponseId] uniqueidentifier NULL,
        [UploadedBySubjectId] nvarchar(200) NOT NULL,
        [Audience] nvarchar(20) NOT NULL,
        [StorageProvider] nvarchar(80) NOT NULL,
        [ObjectKey] nvarchar(500) NOT NULL,
        [OriginalFileName] nvarchar(255) NOT NULL,
        [ContentType] nvarchar(150) NOT NULL,
        [LengthBytes] bigint NOT NULL,
        [Sha256] char(64) NOT NULL,
        [UploadStatus] nvarchar(30) NOT NULL,
        [UploadedUtc] datetimeoffset NOT NULL,
        [DeletedUtc] datetimeoffset NULL,
        CONSTRAINT [PK_ClaimDocuments] PRIMARY KEY ([DocumentId]),
        CONSTRAINT [CK_ClaimDocuments_Audience] CHECK ([Audience] IN (N'Claimant', N'Staff')),
        CONSTRAINT [CK_ClaimDocuments_LengthBytes] CHECK ([LengthBytes] >= 0),
        CONSTRAINT [FK_ClaimDocuments_ClaimInformationResponses_ResponseId] FOREIGN KEY ([ResponseId]) REFERENCES [dbo].[ClaimInformationResponses] ([ResponseId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ClaimDocuments_Claims_ClaimId] FOREIGN KEY ([ClaimId]) REFERENCES [dbo].[Claims] ([ClaimId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_ClaimActivities_ActivityType_OccurredUtc] ON [dbo].[ClaimActivities] ([ActivityType], [OccurredUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_ClaimActivities_ClaimId_OccurredUtc] ON [dbo].[ClaimActivities] ([ClaimId], [OccurredUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_ClaimAssessments_ClaimId_CreatedUtc] ON [dbo].[ClaimAssessments] ([ClaimId], [CreatedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ClaimAssignments_ClaimId] ON [dbo].[ClaimAssignments] ([ClaimId]) WHERE [UnassignedUtc] IS NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_ClaimAssignments_OfficerSubjectId_UnassignedUtc_AssignedUtc] ON [dbo].[ClaimAssignments] ([OfficerSubjectId], [UnassignedUtc], [AssignedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_ClaimAssignments_TeamId_UnassignedUtc_AssignedUtc] ON [dbo].[ClaimAssignments] ([TeamId], [UnassignedUtc], [AssignedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClaimDecisions_ClaimId] ON [dbo].[ClaimDecisions] ([ClaimId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_ClaimDocuments_ClaimId_UploadedUtc] ON [dbo].[ClaimDocuments] ([ClaimId], [UploadedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_ClaimDocuments_ResponseId] ON [dbo].[ClaimDocuments] ([ResponseId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_ClaimInformationRequests_ClaimId_Status_RequestedUtc] ON [dbo].[ClaimInformationRequests] ([ClaimId], [Status], [RequestedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_ClaimInformationResponses_RequestId] ON [dbo].[ClaimInformationResponses] ([RequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_ClaimNotes_ClaimId] ON [dbo].[ClaimNotes] ([ClaimId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_ClaimReserveHistory_ClaimId_RecordedUtc] ON [dbo].[ClaimReserveHistory] ([ClaimId], [RecordedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_Claims_ClaimantSubjectId_CreatedUtc] ON [dbo].[Claims] ([ClaimantSubjectId], [CreatedUtc] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Claims_ClaimNumber] ON [dbo].[Claims] ([ClaimNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_Claims_IncidentOccurredUtc_MarketCode_Status] ON [dbo].[Claims] ([IncidentOccurredUtc], [MarketCode], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_Claims_MarketCode_Status_SubmittedUtc] ON [dbo].[Claims] ([MarketCode], [Status], [SubmittedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_Claims_Status_UpdatedUtc] ON [dbo].[Claims] ([Status], [UpdatedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_Claims_SubmittedUtc_Status_MarketCode] ON [dbo].[Claims] ([SubmittedUtc], [Status], [MarketCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ClaimSettlements_ClaimId] ON [dbo].[ClaimSettlements] ([ClaimId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_PublishedUtc_NextAttemptUtc_CreatedUtc] ON [dbo].[OutboxMessages] ([PublishedUtc], [NextAttemptUtc], [CreatedUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_TeamMarkets_MarketCode] ON [dbo].[TeamMarkets] ([MarketCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE INDEX [IX_TeamMemberships_StaffSubjectId_EffectiveToUtc_TeamId] ON [dbo].[TeamMemberships] ([StaffSubjectId], [EffectiveToUtc], [TeamId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Teams_TeamCode] ON [dbo].[Teams] ([TeamCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261122121945_InitialClaimsSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261122121945_InitialClaimsSchema', N'10.0.14');
END;

COMMIT;
GO

