using System.Text;
using System.Text.Json;
using Chubb.Claims.Application.Abstractions;
using Chubb.Claims.Application.Commands;
using Chubb.Claims.Application.Contracts;
using Chubb.Claims.Application.Queries;
using Chubb.Claims.Domain.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AppValidationException = Chubb.Claims.Application.Abstractions.ValidationException;

namespace Chubb.Claims.Api;

[ApiController]
[Authorize(Policy = "Staff")]
[Route("api/v1/staff/claims")]
public sealed class StaffClaimsController(
    IQueryHandler<ListStaffWorkQueue, PageResult<StaffClaimCard>> queue,
    IQueryHandler<GetStaffClaim, StaffClaimDetail> get,
    ICommandHandler<AssignClaim, ClaimCommandResult> assign,
    ICommandHandler<StartReview, ClaimCommandResult> start,
    ICommandHandler<RequestInformation, ClaimCommandResult> requestInformation,
    ICommandHandler<ResumeReview, ClaimCommandResult> resume,
    ICommandHandler<RecordAssessment, ClaimCommandResult> assess,
    ICommandHandler<RecordFinalDecision, ClaimCommandResult> decide,
    ICommandHandler<AddInternalNote, ClaimCommandResult> note,
    ICommandHandler<CompleteSettlementWorkflow, ClaimCommandResult> settle) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ClaimantPage<StaffClaimCard>>> Queue([FromQuery] string? market,
        [FromQuery] ClaimStatus? status, [FromQuery] string? teamId, [FromQuery] string? officerId,
        [FromQuery] DateTimeOffset? submittedFrom, [FromQuery] DateTimeOffset? submittedTo,
        [FromQuery] DateTimeOffset? incidentFrom, [FromQuery] DateTimeOffset? incidentTo,
        [FromQuery] string? claimNumber, [FromQuery] int pageSize = 20, [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var page = MakePage(pageSize, cursor);
        var filter = new WorkQueueFilter(market, status is null ? null : new HashSet<ClaimStatus> { status.Value },
            teamId, officerId, submittedFrom, submittedTo, claimNumber, incidentFrom, incidentTo);
        var result = await queue.HandleAsync(new ListStaffWorkQueue(filter, page), cancellationToken);
        return Ok(new ClaimantPage<StaffClaimCard>(result.Items, EncodeCursor(result.Next)));
    }

    [HttpGet("{claimId:guid}")]
    public async Task<ActionResult<StaffClaimDetail>> GetClaim(Guid claimId, CancellationToken cancellationToken)
    {
        var result = await get.HandleAsync(new GetStaffClaim(claimId), cancellationToken);
        Response.Headers.ETag = QuoteEtag(result.Version);
        return Ok(result);
    }

    [HttpPost("{id:guid}/assignment")]
    public Task<IActionResult> Assign(Guid id, [FromBody] AssignmentRequest r, CancellationToken ct) => Mutate(
        assign.HandleAsync(new AssignClaim(id, ReadEtag(), r.TeamId, r.OfficerSubjectId, r.Reason), ct));
    [HttpPost("{id:guid}/review")]
    [Authorize(Policy = "ClaimsOfficer")]
    public Task<IActionResult> StartReview(Guid id, CancellationToken ct) => Mutate(start.HandleAsync(new StartReview(id, ReadEtag()), ct));
    [HttpPost("{id:guid}/information-requests")]
    [Authorize(Policy = "ClaimsOfficer")]
    public Task<IActionResult> RequestInformation(Guid id, [FromBody] InformationRequest r, CancellationToken ct) => Mutate(
        requestInformation.HandleAsync(new RequestInformation(id, ReadEtag(), r.Question), ct));
    [HttpPost("{id:guid}/resume-review")]
    [Authorize(Policy = "ClaimsOfficer")]
    public Task<IActionResult> ResumeReview(Guid id, CancellationToken ct) => Mutate(resume.HandleAsync(new ResumeReview(id, ReadEtag()), ct));
    [HttpPost("{id:guid}/assessments")]
    [Authorize(Policy = "ClaimsOfficer")]
    public Task<IActionResult> RecordAssessment(Guid id, [FromBody] AssessmentRequest r, CancellationToken ct) => Mutate(
        assess.HandleAsync(new RecordAssessment(id, ReadEtag(), r.AssessmentType, r.Rationale, r.Recommendation, r.ReserveEstimate, r.ReserveReason), ct));
    [HttpPost("{id:guid}/decision")]
    [Authorize(Policy = "ClaimsOfficer")]
    public Task<IActionResult> Decide(Guid id, [FromBody] DecisionRequest r, CancellationToken ct) => Mutate(
        decide.HandleAsync(new RecordFinalDecision(id, ReadEtag(), r.Type, r.Reason, r.Amount), ct));
    [HttpPost("{id:guid}/internal-notes")]
    [Authorize(Policy = "ClaimsOfficer")]
    public Task<IActionResult> AddNote(Guid id, [FromBody] NoteRequest r, CancellationToken ct) => Mutate(
        note.HandleAsync(new AddInternalNote(id, ReadEtag(), r.NoteText), ct));
    [HttpPost("{id:guid}/settlement-workflow")]
    [Authorize(Policy = "ClaimsOfficer")]
    public Task<IActionResult> CompleteSettlement(Guid id, [FromBody] SettlementRequest r, CancellationToken ct) => Mutate(
        settle.HandleAsync(new CompleteSettlementWorkflow(id, ReadEtag(), r.Amount, r.Reference), ct));

    private async Task<IActionResult> Mutate(Task<ClaimCommandResult> action)
    {
        var result = await action;
        Response.Headers.ETag = QuoteEtag(result.Version);
        return Ok(new ClaimCommandResponse(result.ClaimId, result.ClaimNumber, result.Status, result.Version));
    }
    private string ReadEtag()
    {
        var value = Request.Headers.IfMatch.ToString().Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
        if (string.IsNullOrWhiteSpace(value)) throw new AppValidationException("If-Match with the claim ETag is required.");
        return value;
    }
    private static string QuoteEtag(string version) => $"\"{version}\"";
    private static PageRequest MakePage(int size, string? cursor)
    {
        if (size is < 1 or > 100) throw new AppValidationException("pageSize must be between 1 and 100.");
        if (string.IsNullOrWhiteSpace(cursor)) return new(size);
        try
        {
            var value = JsonSerializer.Deserialize<PageCursor>(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)));
            if (value is null || value.Id == Guid.Empty) throw new FormatException();
            return new(size, value);
        }
        catch (Exception ex) when (ex is FormatException or JsonException) { throw new AppValidationException("cursor is invalid."); }
    }
    private static string? EncodeCursor(PageCursor? cursor) => cursor is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cursor)));
}

public sealed record AssignmentRequest(string TeamId, string? OfficerSubjectId, string? Reason);
public sealed record InformationRequest(string Question);
public sealed record AssessmentRequest(string AssessmentType, string Rationale, string? Recommendation, MoneyEstimateInput? ReserveEstimate, string? ReserveReason);
public sealed record DecisionRequest(DecisionType Type, string Reason, MoneyEstimateInput? Amount);
public sealed record NoteRequest(string NoteText);
public sealed record SettlementRequest(MoneyEstimateInput? Amount, string? Reference);
public sealed record ClaimCommandResponse(Guid ClaimId, string ClaimNumber, ClaimStatus Status, string Version);
