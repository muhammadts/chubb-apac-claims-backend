using System.ComponentModel.DataAnnotations;
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
[Authorize(Policy = "Claimant")]
[Route("api/v1/claims")]
public sealed class ClaimantClaimsController(
    ICommandHandler<SubmitClaim, ClaimCommandResult> submit,
    ICommandHandler<SubmitInformationResponse, ClaimCommandResult> respond,
    IQueryHandler<ListClaimantClaims, PageResult<ClaimantClaimCard>> list,
    IQueryHandler<GetClaimantClaim, ClaimantClaimDetail> get,
    IQueryHandler<GetClaimantHistory, PageResult<ClaimantHistoryItem>> history) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ClaimSubmissionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Submit([FromBody] SubmitClaimRequest request, CancellationToken cancellationToken)
    {
        ClaimDetailsInput details = request.LineOfBusiness switch
        {
            LineOfBusiness.Motor when request.Motor is not null && request.Property is null => request.Motor,
            LineOfBusiness.Property when request.Property is not null && request.Motor is null => request.Property,
            _ => throw new AppValidationException("Provide details matching the selected lineOfBusiness only.")
        };
        var result = await submit.HandleAsync(new SubmitClaim(Guid.NewGuid(), request.MarketCode,
            request.LineOfBusiness, new ClaimantContactInput(request.Contact.Name, request.Contact.Email, request.Contact.Phone),
            new IncidentInput(request.Incident.OccurredAt ?? throw new AppValidationException("incident.occurredAt is required."), request.Incident.Description), details,
            request.ReportedAt ?? DateTimeOffset.UtcNow), cancellationToken);
        Response.Headers.ETag = QuoteEtag(result.Version);
        return CreatedAtAction(nameof(GetClaim), new { claimId = result.ClaimId },
            new ClaimSubmissionResponse(result.ClaimId, result.ClaimNumber, result.Status, result.Version));
    }

    [HttpGet]
    public async Task<ActionResult<ClaimantPage<ClaimantClaimCard>>> List([FromQuery] int pageSize = 20,
        [FromQuery] string? cursor = null, CancellationToken cancellationToken = default)
    {
        var request = MakePage(pageSize, cursor);
        var result = await list.HandleAsync(new ListClaimantClaims(request.Page), cancellationToken);
        return Ok(new ClaimantPage<ClaimantClaimCard>(result.Items, EncodeCursor(result.Next)));
    }

    [HttpGet("{claimId:guid}")]
    public async Task<ActionResult<ClaimantClaimDetail>> GetClaim(Guid claimId, CancellationToken cancellationToken)
    {
        var result = await get.HandleAsync(new GetClaimantClaim(claimId), cancellationToken);
        Response.Headers.ETag = QuoteEtag(result.Version);
        return Ok(result);
    }

    [HttpGet("{claimId:guid}/status")]
    public async Task<ActionResult<ClaimantStatusResponse>> GetStatus(Guid claimId, CancellationToken cancellationToken)
    {
        var result = await get.HandleAsync(new GetClaimantClaim(claimId), cancellationToken);
        Response.Headers.ETag = QuoteEtag(result.Version);
        return Ok(new ClaimantStatusResponse(result.ClaimId, result.ClaimNumber, result.Status, result.SubmittedAt));
    }

    [HttpGet("{claimId:guid}/timeline")]
    public async Task<ActionResult<ClaimantPage<ClaimantHistoryItem>>> GetTimeline(Guid claimId,
        [FromQuery] int pageSize = 20, [FromQuery] string? cursor = null, CancellationToken cancellationToken = default)
    {
        var request = MakePage(pageSize, cursor);
        var result = await history.HandleAsync(new GetClaimantHistory(claimId, request.Page), cancellationToken);
        return Ok(new ClaimantPage<ClaimantHistoryItem>(result.Items, EncodeCursor(result.Next)));
    }

    [HttpPost("{claimId:guid}/information-requests/{requestId:guid}/responses")]
    [ProducesResponseType<ClaimantResponseSubmission>(StatusCodes.Status201Created)]
    public async Task<IActionResult> SubmitResponse(Guid claimId, Guid requestId,
        [FromBody] SubmitInformationResponseRequest request, CancellationToken cancellationToken)
    {
        // Resolve ownership before the write-side version check so an unowned claim cannot be probed by ETag.
        _ = await get.HandleAsync(new GetClaimantClaim(claimId), cancellationToken);
        var version = ReadEtag();
        var result = await respond.HandleAsync(new SubmitInformationResponse(claimId, version, requestId,
            request.ResponseText?.Trim()), cancellationToken);
        Response.Headers.ETag = QuoteEtag(result.Version);
        return StatusCode(StatusCodes.Status201Created,
            new ClaimantResponseSubmission(result.ClaimId, requestId, result.Status, result.Version));
    }

    private string ReadEtag()
    {
        var value = Request.Headers.IfMatch.ToString().Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
        if (string.IsNullOrWhiteSpace(value)) throw new AppValidationException("If-Match with the claim ETag is required.");
        return value;
    }

    private static string QuoteEtag(string version) => $"\"{version}\"";
    private static PageInput MakePage(int pageSize, string? cursor)
    {
        if (pageSize is < 1 or > 100) throw new AppValidationException("pageSize must be between 1 and 100.");
        if (string.IsNullOrWhiteSpace(cursor)) return new(new PageRequest(pageSize));
        try
        {
            var decoded = JsonSerializer.Deserialize<PageCursor>(Encoding.UTF8.GetString(Convert.FromBase64String(cursor)));
            if (decoded is null || decoded.Id == Guid.Empty) throw new FormatException();
            return new(new PageRequest(pageSize, decoded));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new AppValidationException("cursor is invalid.");
        }
    }
    private static string? EncodeCursor(PageCursor? cursor) => cursor is null ? null : Convert.ToBase64String(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cursor)));
    private sealed record PageInput(PageRequest Page);
}

public sealed record SubmitClaimRequest
{
    [Required, StringLength(3, MinimumLength = 3)] public string MarketCode { get; init; } = "";
    [Required] public LineOfBusiness LineOfBusiness { get; init; }
    [Required] public ClaimantContactRequest Contact { get; init; } = new("", "", null);
    [Required] public IncidentRequest Incident { get; init; } = new(default, "");
    public DateTimeOffset? ReportedAt { get; init; }
    public MotorClaimDetailsInput? Motor { get; init; }
    public PropertyClaimDetailsInput? Property { get; init; }
}
public sealed record ClaimantContactRequest([Required, StringLength(200)] string Name,
    [Required, EmailAddress, StringLength(320)] string Email, [StringLength(50)] string? Phone);
public sealed record IncidentRequest([Required] DateTimeOffset? OccurredAt,
    [Required, StringLength(4000, MinimumLength = 1)] string Description);
public sealed record SubmitInformationResponseRequest([StringLength(4000)] string? ResponseText);
public sealed record ClaimSubmissionResponse(Guid ClaimId, string ClaimNumber, ClaimStatus Status, string Version);
public sealed record ClaimantResponseSubmission(Guid ClaimId, Guid RequestId, ClaimStatus Status, string Version);
public sealed record ClaimantStatusResponse(Guid ClaimId, string ClaimNumber, ClaimStatus Status, DateTimeOffset SubmittedAt);
public sealed record ClaimantPage<T>(IReadOnlyList<T> Items, string? NextCursor);
