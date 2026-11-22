using Chubb.Claims.Application.Abstractions;
using Chubb.Claims.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chubb.Claims.Api;

[ApiController]
[Authorize(Policy = "Manager")]
[Route("api/v1/manager")]
public sealed class ManagerReportsController(
    IQueryHandler<GetManagerWorkload, ManagerWorkloadSummary> workload,
    IQueryHandler<GetManagerPerformance, ManagerPerformanceSummary> performance,
    IQueryHandler<GetManagerExposure, ManagerExposureSummary> exposure) : ControllerBase
{
    [HttpGet("workload")]
    public async Task<ActionResult<ManagerWorkloadSummary>> Workload(CancellationToken ct) => Ok(await workload.HandleAsync(new(), ct));

    [HttpGet("performance")]
    public async Task<ActionResult<ManagerPerformanceSummary>> Performance([FromQuery] DateTimeOffset fromUtc,
        [FromQuery] DateTimeOffset toUtc, CancellationToken ct) => Ok(await performance.HandleAsync(new(fromUtc, toUtc), ct));

    [HttpGet("exposure")]
    public async Task<ActionResult<ManagerExposureSummary>> Exposure(CancellationToken ct) => Ok(await exposure.HandleAsync(new(), ct));
}
