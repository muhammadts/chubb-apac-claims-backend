namespace Chubb.Claims.Domain.Claims;

public sealed record ClaimAssignment(
    string TeamId,
    string? OfficerSubjectId,
    string AssignedBySubjectId,
    DateTimeOffset AssignedAt,
    string? Reason = null);

public sealed record ClaimAssessment(
    string AssessorSubjectId,
    DateTimeOffset RecordedAt,
    string AssessmentType,
    string Rationale,
    string? Recommendation = null);

public sealed record ClaimDecision(
    DecisionType Type,
    string Reason,
    string DecidedBySubjectId,
    DateTimeOffset DecidedAt,
    MoneyEstimate? Amount = null);

public sealed record ClaimSettlement(
    string RecordedBySubjectId,
    DateTimeOffset CompletedAt,
    MoneyEstimate? Amount = null,
    string? Reference = null);

public sealed record ClaimInformationResponse(
    string SubmittedBySubjectId,
    DateTimeOffset SubmittedAt,
    string? ResponseText);

public sealed class ClaimInformationRequest
{
    private ClaimInformationRequest(Guid id, string requestedBySubjectId, string question, DateTimeOffset requestedAt)
    {
        Id = id;
        RequestedBySubjectId = requestedBySubjectId;
        Question = question;
        RequestedAt = requestedAt;
    }

    public Guid Id { get; }
    public string RequestedBySubjectId { get; }
    public string Question { get; }
    public DateTimeOffset RequestedAt { get; }
    public ClaimInformationResponse? Response { get; private set; }
    public bool IsOpen => Response is null;

    internal static ClaimInformationRequest Create(string actor, string question, DateTimeOffset at) =>
        new(Guid.NewGuid(), actor, question, at);

    internal void Respond(ClaimInformationResponse response) => Response = response;
}
