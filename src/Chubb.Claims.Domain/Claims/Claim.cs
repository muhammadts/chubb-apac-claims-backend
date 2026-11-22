namespace Chubb.Claims.Domain.Claims;

/// <summary>The write-side domain aggregate for one submitted claim.</summary>
public sealed class Claim
{
    private readonly List<ClaimInformationRequest> _informationRequests = [];
    private readonly List<ClaimAssessment> _assessments = [];

    public Claim(
        Guid id,
        string claimNumber,
        string claimantSubjectId,
        ClaimantContactSnapshot claimantContact,
        string marketCode,
        LineOfBusiness lineOfBusiness,
        IncidentFacts incident,
        ClaimDetails details,
        DateTimeOffset reportedAt,
        DateTimeOffset submittedAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Claim ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(claimNumber)) throw new ArgumentException("Claim number is required.", nameof(claimNumber));
        if (string.IsNullOrWhiteSpace(claimantSubjectId)) throw new ArgumentException("Claimant subject is required.", nameof(claimantSubjectId));
        if (string.IsNullOrWhiteSpace(marketCode)) throw new ArgumentException("Market code is required.", nameof(marketCode));
        ArgumentNullException.ThrowIfNull(claimantContact);
        ArgumentNullException.ThrowIfNull(incident);
        ArgumentNullException.ThrowIfNull(details);

        if ((lineOfBusiness, details) is not (LineOfBusiness.Motor, MotorClaimDetails)
            and not (LineOfBusiness.Property, PropertyClaimDetails))
        {
            throw new ArgumentException("Claim details must match the line of business.", nameof(details));
        }

        Id = id;
        ClaimNumber = claimNumber.Trim();
        ClaimantSubjectId = claimantSubjectId.Trim();
        ClaimantContact = claimantContact;
        MarketCode = marketCode.Trim();
        LineOfBusiness = lineOfBusiness;
        Incident = incident;
        Details = details;
        ReportedAt = reportedAt;
        SubmittedAt = submittedAt;
        Status = ClaimStatus.Submitted;
    }

    /// <summary>Rebuilds a claim aggregate from its normalized persistence records.</summary>
    public static Claim Rehydrate(Guid id, string claimNumber, string claimantSubjectId,
        ClaimantContactSnapshot claimantContact, string marketCode, LineOfBusiness lineOfBusiness,
        IncidentFacts incident, ClaimDetails details, DateTimeOffset reportedAt, DateTimeOffset submittedAt,
        ClaimStatus status, ClaimAssignment? assignment, IEnumerable<ClaimInformationRequest> requests,
        IEnumerable<ClaimAssessment> assessments, MoneyEstimate? reserveEstimate, ClaimDecision? decision,
        ClaimSettlement? settlement)
    {
        var claim = new Claim(id, claimNumber, claimantSubjectId, claimantContact, marketCode,
            lineOfBusiness, incident, details, reportedAt, submittedAt)
        {
            Status = status,
            Assignment = assignment,
            ReserveEstimate = reserveEstimate,
            Decision = decision,
            Settlement = settlement
        };
        claim._informationRequests.AddRange(requests);
        claim._assessments.AddRange(assessments);
        return claim;
    }

    public Guid Id { get; }
    public string ClaimNumber { get; }
    public string ClaimantSubjectId { get; }
    public ClaimantContactSnapshot ClaimantContact { get; }
    public string MarketCode { get; }
    public LineOfBusiness LineOfBusiness { get; }
    public IncidentFacts Incident { get; }
    public ClaimDetails Details { get; }
    public DateTimeOffset ReportedAt { get; }
    public DateTimeOffset SubmittedAt { get; }
    public ClaimStatus Status { get; private set; }
    public ClaimAssignment? Assignment { get; private set; }
    public IReadOnlyList<ClaimInformationRequest> InformationRequests => _informationRequests.AsReadOnly();
    public IReadOnlyList<ClaimAssessment> Assessments => _assessments.AsReadOnly();
    public MoneyEstimate? ReserveEstimate { get; private set; }
    public ClaimDecision? Decision { get; private set; }
    public ClaimSettlement? Settlement { get; private set; }

    public void Assign(ClaimAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        if (string.IsNullOrWhiteSpace(assignment.TeamId) || string.IsNullOrWhiteSpace(assignment.AssignedBySubjectId))
        {
            throw new ArgumentException("A team and assigning staff subject are required.", nameof(assignment));
        }

        Assignment = assignment;
    }

    public void StartReview(string staffSubjectId)
    {
        RequireActor(staffSubjectId, nameof(staffSubjectId));
        RequireStatus(ClaimStatus.Submitted);
        Status = ClaimStatus.InReview;
    }

    public void RecordAssessment(ClaimAssessment assessment, MoneyEstimate? reserveEstimate = null)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        RequireOneOfStatuses(ClaimStatus.InReview, ClaimStatus.Assessment);
        _assessments.Add(assessment);
        if (reserveEstimate is not null) ReserveEstimate = reserveEstimate;
        Status = ClaimStatus.Assessment;
    }

    public Guid RequestInformation(string staffSubjectId, string question, DateTimeOffset requestedAt)
    {
        RequireActor(staffSubjectId, nameof(staffSubjectId));
        RequireOneOfStatuses(ClaimStatus.InReview, ClaimStatus.Assessment);
        if (string.IsNullOrWhiteSpace(question)) throw new ArgumentException("A question is required.", nameof(question));

        var request = ClaimInformationRequest.Create(staffSubjectId.Trim(), question.Trim(), requestedAt);
        _informationRequests.Add(request);
        Status = ClaimStatus.AwaitingInformation;
        return request.Id;
    }

    public void SubmitInformationResponse(Guid requestId, string claimantSubjectId, string? responseText, DateTimeOffset submittedAt)
    {
        RequireActor(claimantSubjectId, nameof(claimantSubjectId));
        if (!string.Equals(ClaimantSubjectId, claimantSubjectId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only the owning claimant can respond to an information request.");
        }

        RequireStatus(ClaimStatus.AwaitingInformation);
        var request = _informationRequests.SingleOrDefault(item => item.Id == requestId)
            ?? throw new InvalidOperationException("The information request does not belong to this claim.");
        if (!request.IsOpen) throw new InvalidOperationException("The information request has already been answered.");

        request.Respond(new ClaimInformationResponse(claimantSubjectId.Trim(), submittedAt, responseText?.Trim()));
    }

    public void ResumeReview(string staffSubjectId)
    {
        RequireActor(staffSubjectId, nameof(staffSubjectId));
        RequireStatus(ClaimStatus.AwaitingInformation);
        if (_informationRequests.Count == 0 || _informationRequests.Any(request => request.IsOpen))
        {
            throw new InvalidOperationException("Staff can resume review after all information requests have responses.");
        }

        Status = ClaimStatus.InReview;
    }

    public void RecordDecision(ClaimDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        RequireStatus(ClaimStatus.Assessment);
        if (Decision is not null) throw new InvalidOperationException("A final decision has already been recorded.");

        Decision = decision;
        Status = decision.Type == DecisionType.Approved ? ClaimStatus.Approved : ClaimStatus.Rejected;
    }

    public void CompleteSettlement(ClaimSettlement settlement)
    {
        ArgumentNullException.ThrowIfNull(settlement);
        RequireStatus(ClaimStatus.Approved);
        if (Decision?.Type != DecisionType.Approved)
        {
            throw new InvalidOperationException("Only an approved claim can be settled.");
        }

        Settlement = settlement;
        Status = ClaimStatus.Settled;
    }

    public void UpdateReserveEstimate(MoneyEstimate estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        ReserveEstimate = estimate;
    }

    private void RequireStatus(ClaimStatus expected)
    {
        if (Status != expected) throw new InvalidOperationException($"This action requires status {expected}; current status is {Status}.");
    }

    private void RequireOneOfStatuses(params ClaimStatus[] expected)
    {
        if (!expected.Contains(Status)) throw new InvalidOperationException($"This action is not valid while status is {Status}.");
    }

    private static void RequireActor(string actor, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Actor subject is required.", parameterName);
    }
}
