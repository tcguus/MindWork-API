using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindWork.Api.Domain.Entities;
using MindWork.Api.Domain.Enums;
using MindWork.Api.Infrastructure.Persistence;
using MindWork.Api.Models.Responses;

namespace MindWork.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class SelfAssessmentsController : ControllerBase
{
    private readonly MindWorkDbContext _dbContext;

    public SelfAssessmentsController(MindWorkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    [ProducesResponseType(typeof(SelfAssessmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SelfAssessmentResponse>> CreateAsync([FromBody] CreateSelfAssessmentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetCurrentUserId();

        var entity = new SelfAssessment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            Mood = (MoodLevel)request.Mood,
            Stress = (StressLevel)request.Stress,
            Workload = (WorkloadLevel)request.Workload,
            Notes = request.Notes
        };

        _dbContext.SelfAssessments.Add(entity);
        await _dbContext.SaveChangesAsync();

        var response = new SelfAssessmentResponse(
            entity.Id,
            entity.CreatedAt,
            (int)entity.Mood,
            (int)entity.Stress,
            (int)entity.Workload,
            entity.Notes
        );
        
        return CreatedAtAction(
            nameof(GetById), 
            new { id = entity.Id, version = "1" }, 
            response);
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(PagedResponse<SelfAssessmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResponse<SelfAssessmentResponse>>> GetMyAsync(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = GetCurrentUserId();

        var query = _dbContext.SelfAssessments
            .Where(sa => sa.UserId == userId)
            .OrderByDescending(sa => sa.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(sa => new SelfAssessmentResponse(
                sa.Id,
                sa.CreatedAt,
                (int)sa.Mood,
                (int)sa.Stress,
                (int)sa.Workload,
                sa.Notes
            ))
            .ToListAsync();

        var response = new PagedResponse<SelfAssessmentResponse>(
            items,
            pageNumber,
            pageSize,
            totalCount
        );

        response.Links.Add(new Link(Url.ActionLink(nameof(GetMyAsync), values: new { pageNumber, pageSize })!, "self", "GET"));

        if (pageNumber * pageSize < totalCount)
        {
            response.Links.Add(new Link(Url.ActionLink(nameof(GetMyAsync), values: new { pageNumber = pageNumber + 1, pageSize })!, "next", "GET"));
        }

        if (pageNumber > 1)
        {
            response.Links.Add(new Link(Url.ActionLink(nameof(GetMyAsync), values: new { pageNumber = pageNumber - 1, pageSize })!, "previous", "GET"));
        }

        return Ok(response);
    }
    
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SelfAssessmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SelfAssessmentResponse>> GetById(Guid id)
    {
        var userId = GetCurrentUserId();

        var sa = await _dbContext.SelfAssessments
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (sa is null)
            return NotFound(new { message = "Autoavaliação não encontrada." });

        var response = new SelfAssessmentResponse(
            sa.Id,
            sa.CreatedAt,
            (int)sa.Mood,
            (int)sa.Stress,
            (int)sa.Workload,
            sa.Notes
        );

        return Ok(response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAsync(Guid id, [FromBody] UpdateSelfAssessmentRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var userId = GetCurrentUserId();
        var sa = await _dbContext.SelfAssessments.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (sa is null) return NotFound(new { message = "Autoavaliação não encontrada." });

        sa.Mood = (MoodLevel)request.Mood;
        sa.Stress = (StressLevel)request.Stress;
        sa.Workload = (WorkloadLevel)request.Workload;
        sa.Notes = request.Notes;
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        var userId = GetCurrentUserId();
        var sa = await _dbContext.SelfAssessments.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (sa is null) return NotFound(new { message = "Autoavaliação não encontrada." });

        _dbContext.SelfAssessments.Remove(sa);
        await _dbContext.SaveChangesAsync();
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(sub!);
    }
}

public record CreateSelfAssessmentRequest(int Mood, int Stress, int Workload, string? Notes);
public record UpdateSelfAssessmentRequest(int Mood, int Stress, int Workload, string? Notes);
public record SelfAssessmentResponse(Guid Id, DateTime CreatedAt, int Mood, int Stress, int Workload, string? Notes);
