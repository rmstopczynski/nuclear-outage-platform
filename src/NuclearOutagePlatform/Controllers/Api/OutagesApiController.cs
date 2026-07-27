using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MVC_EF_Start_8.Models;
using MVC_EF_Start_8.Models.Api;
using MVC_EF_Start_8.Services;

namespace MVC_EF_Start_8.Controllers.Api
{
    /// <summary>
    /// JSON REST API for outage data, distinct from the Razor/MVC pages
    /// in HomeController -- see the README's "REST API" section. This is
    /// the piece the Nuclear Intelligence Platform plan calls out as a
    /// separate architecture box from the web dashboard: something a
    /// non-browser client (a script, another service, the AI summary
    /// feature in Step 5, or an MCP server in Step 6) can call directly.
    ///
    /// Read endpoints are anonymous, matching the existing MVC pages'
    /// permissions. Write endpoints (POST/PUT/DELETE) are anonymous too
    /// for now, same as the existing MVC Create/Update/Delete actions --
    /// not a new gap, just not yet tightened. Locking these down (e.g. to
    /// authenticated users, or a separate API-key scheme for
    /// machine clients) is a reasonable next hardening step, not done here
    /// to keep this step's scope to "add the API layer."
    /// </summary>
    [ApiController]
    [Route("api/outages")]
    public class OutagesApiController : ControllerBase
    {
        private const int MaxPageSize = 200;

        private readonly OutageService _outageService;
        private readonly OutageSummaryService _summaryService;
        private readonly ILogger<OutagesApiController> _logger;

        public OutagesApiController(
            OutageService outageService,
            OutageSummaryService summaryService,
            ILogger<OutagesApiController> logger)
        {
            _outageService = outageService;
            _summaryService = summaryService;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/outages?facility=...&amp;from=...&amp;to=...&amp;page=1&amp;pageSize=50
        /// Facility matches against both the short code and the full name
        /// (same matching OutageService.SearchOutagesAsync already used
        /// for the dashboard's search box).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<OutageDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOutages(
            [FromQuery] string? facility,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (page < 1)
                return BadRequest(new ProblemDetails { Title = "page must be 1 or greater." });
            if (pageSize < 1 || pageSize > MaxPageSize)
                return BadRequest(new ProblemDetails { Title = $"pageSize must be between 1 and {MaxPageSize}." });
            if (from.HasValue && to.HasValue && from > to)
                return BadRequest(new ProblemDetails { Title = "from must not be after to." });

            try
            {
                var (items, totalCount) = await _outageService.QueryAsync(facility, from, to, page, pageSize);

                return Ok(new PagedResult<OutageDto>
                {
                    Items = items.ConvertAll(OutageDto.FromEntity),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET /api/outages failed.");
                return Problem("An unexpected error occurred while querying outages.", statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>GET /api/outages/{id}</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(OutageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var record = await _outageService.GetByIdAsync(id);
            if (record == null)
                return NotFound(new ProblemDetails { Title = $"No outage record with id {id}." });

            return Ok(OutageDto.FromEntity(record));
        }

        /// <summary>
        /// GET /api/outages/trends?facility=...&amp;from=...&amp;to=...
        /// Daily total outage MW, optionally scoped to one facility and/or
        /// a date range -- the "analyze trends" surface the plan calls
        /// for, reusable by the dashboard, the AI summary feature, or an
        /// MCP tool.
        /// </summary>
        [HttpGet("trends")]
        [ProducesResponseType(typeof(TrendResponseDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTrends(
            [FromQuery] string? facility,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to)
        {
            if (from.HasValue && to.HasValue && from > to)
                return BadRequest(new ProblemDetails { Title = "from must not be after to." });

            try
            {
                var points = await _outageService.GetDailyTrendAsync(facility, from, to);

                return Ok(new TrendResponseDto
                {
                    Facility = facility,
                    From = from?.ToString("yyyy-MM-dd"),
                    To = to?.ToString("yyyy-MM-dd"),
                    Points = points.ConvertAll(p => new TrendPointDto
                    {
                        Period = p.Period.ToString("yyyy-MM-dd"),
                        TotalOutage = p.TotalOutage,
                        RecordCount = p.RecordCount,
                    }),
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET /api/outages/trends failed.");
                return Problem("An unexpected error occurred while computing trends.", statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// GET /api/outages/summary?facility=...&amp;from=...&amp;to=...
        /// The one AI feature in this project -- a bounded Groq API call
        /// over the same filtered data GET /api/outages/trends uses,
        /// producing a short plain-English summary. Defaults to the last
        /// 7 days if from/to aren't given.
        /// </summary>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(OutageSummaryResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> GetSummary(
            [FromQuery] string? facility,
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to)
        {
            if (from.HasValue && to.HasValue && from > to)
                return BadRequest(new ProblemDetails { Title = "from must not be after to." });

            try
            {
                var (summary, recordCount) = await _summaryService.GenerateSummaryAsync(facility, from, to);

                return Ok(new OutageSummaryResponseDto
                {
                    Summary = summary,
                    Facility = facility,
                    From = from?.ToString("yyyy-MM-dd"),
                    To = to?.ToString("yyyy-MM-dd"),
                    RecordCount = recordCount,
                    GeneratedAt = DateTime.UtcNow.ToString("o"),
                });
            }
            catch (AiSummaryNotConfiguredException ex)
            {
                return Problem(ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (AiProviderUnavailableException ex)
            {
                _logger.LogWarning(ex, "AI summary provider unavailable for GET /api/outages/summary.");
                return Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GET /api/outages/summary failed unexpectedly.");
                return Problem("An unexpected error occurred while generating the summary.", statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>POST /api/outages</summary>
        [HttpPost]
        [ProducesResponseType(typeof(OutageDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] OutageWriteRequest request)
        {
            // [ApiController] already returns 400 automatically for
            // ModelState failures (missing Period/Facility, out-of-range
            // values, etc.) before this method body even runs.
            try
            {
                var record = new OutageRecord
                {
                    Period = request.Period,
                    Facility = request.Facility,
                    FacilityName = request.FacilityName,
                    Generator = request.Generator,
                    Capacity = request.Capacity,
                    Outage = request.Outage,
                    PercentOutage = request.PercentOutage,
                };

                var created = await _outageService.CreateAsync(record);
                var dto = OutageDto.FromEntity(created);
                return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "POST /api/outages failed.");
                return Problem("An unexpected error occurred while creating the outage record.", statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>PUT /api/outages/{id}</summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(OutageDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] OutageWriteRequest request)
        {
            try
            {
                var updated = new OutageRecord
                {
                    Period = request.Period,
                    Facility = request.Facility,
                    FacilityName = request.FacilityName,
                    Generator = request.Generator,
                    Capacity = request.Capacity,
                    Outage = request.Outage,
                    PercentOutage = request.PercentOutage,
                };

                var success = await _outageService.UpdateAsync(id, updated);
                if (!success)
                    return NotFound(new ProblemDetails { Title = $"No outage record with id {id}." });

                var record = await _outageService.GetByIdAsync(id);
                return Ok(OutageDto.FromEntity(record!));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PUT /api/outages/{Id} failed.", id);
                return Problem("An unexpected error occurred while updating the outage record.", statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>DELETE /api/outages/{id}</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var success = await _outageService.DeleteAsync(id);
                if (!success)
                    return NotFound(new ProblemDetails { Title = $"No outage record with id {id}." });

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DELETE /api/outages/{Id} failed.", id);
                return Problem("An unexpected error occurred while deleting the outage record.", statusCode: StatusCodes.Status500InternalServerError);
            }
        }
    }
}
