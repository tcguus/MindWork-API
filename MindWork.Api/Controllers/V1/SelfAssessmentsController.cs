using System.IdentityModel.Tokens.Jwt;
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
[Authorize] // exige JWT
public class SelfAssessmentsController : ControllerBase
{
    private readonly MindWorkDbContext _dbContext;

    public SelfAssessmentsController(MindWorkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Cria uma nova autoavaliação do usuário autenticado.
    /// Usado pelo app mobile para o colaborador registrar humor/estresse/carga.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SelfAssessmentItemResponse>> CreateSelfAssessment(
        [FromBody] CreateSelfAssessmentRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var entity = new SelfAssessment
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            CreatedAt = DateTime.UtcNow,
            Mood = request.Mood,
            Stress = request.Stress,
            Workload = request.Workload,
            Notes = request.Notes
        };

        await _dbContext.SelfAssessments.AddAsync(entity);
        await _dbContext.SaveChangesAsync();

        var response = new SelfAssessmentItemResponse(
            Id: entity.Id,
            CreatedAt: entity.CreatedAt,
            Mood: entity.Mood,
            Stress: entity.Stress,
            Workload: entity.Workload,
            Notes: entity.Notes
        );

        return CreatedAtAction(
            nameof(GetById),
            new { version = "1", id = entity.Id },
            response);
    }

    /// <summary>
    /// Retorna os detalhes de uma autoavaliação específica do usuário autenticado.
    /// Ex.: GET /api/v1/selfassessments/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SelfAssessmentItemResponse>> GetById(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var entity = await _dbContext.SelfAssessments
            .AsNoTracking()
            .FirstOrDefaultAsync(sa => sa.Id == id && sa.UserId == userId.Value);

        if (entity == null)
        {
            return NotFound();
        }

        var response = new SelfAssessmentItemResponse(
            Id: entity.Id,
            CreatedAt: entity.CreatedAt,
            Mood: entity.Mood,
            Stress: entity.Stress,
            Workload: entity.Workload,
            Notes: entity.Notes
        );

        return Ok(response);
    }

    /// <summary>
    /// Retorna as autoavaliações do usuário autenticado, com paginação e HATEOAS.
    /// Ex.: GET /api/v1/selfassessments/my?pageNumber=1&pageSize=10
    /// </summary>
    [HttpGet("my")]
    public async Task<ActionResult<PagedResponse<SelfAssessmentItemResponse>>> GetMySelfAssessments(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0 || pageSize > 50) pageSize = 10;

        var query = _dbContext.SelfAssessments
            .AsNoTracking()
            .Where(sa => sa.UserId == userId.Value)
            .OrderByDescending(sa => sa.CreatedAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var itemResponses = items.Select(sa => new SelfAssessmentItemResponse(
            Id: sa.Id,
            CreatedAt: sa.CreatedAt,
            Mood: sa.Mood,
            Stress: sa.Stress,
            Workload: sa.Workload,
            Notes: sa.Notes
        ));

        var pagedResponse = new PagedResponse<SelfAssessmentItemResponse>(
            items: itemResponses,
            pageNumber: pageNumber,
            pageSize: pageSize,
            totalCount: totalCount);

        // ---------------------------
        // HATEOAS links
        // ---------------------------

        // link para a própria página (self)
        var selfUrl = Url.ActionLink(
            action: nameof(GetMySelfAssessments),
            controller: "SelfAssessments",
            values: new { version = "1", pageNumber, pageSize });

        if (selfUrl != null)
        {
            pagedResponse.AddLink(selfUrl, "self", "GET");
        }

        // link para próxima página, se houver
        if (pagedResponse.HasNext)
        {
            var nextUrl = Url.ActionLink(
                action: nameof(GetMySelfAssessments),
                controller: "SelfAssessments",
                values: new { version = "1", pageNumber = pageNumber + 1, pageSize });

            if (nextUrl != null)
            {
                pagedResponse.AddLink(nextUrl, "next", "GET");
            }
        }

        // link para página anterior, se houver
        if (pagedResponse.HasPrevious)
        {
            var prevUrl = Url.ActionLink(
                action: nameof(GetMySelfAssessments),
                controller: "SelfAssessments",
                values: new { version = "1", pageNumber = pageNumber - 1, pageSize });

            if (prevUrl != null)
            {
                pagedResponse.AddLink(prevUrl, "previous", "GET");
            }
        }

        return Ok(pagedResponse);
    }

    /// <summary>
    /// Atualiza uma autoavaliação do usuário autenticado.
    /// Ex.: PUT /api/v1/selfassessments/{id}
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SelfAssessmentItemResponse>> UpdateSelfAssessment(
        Guid id,
        [FromBody] UpdateSelfAssessmentRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var entity = await _dbContext.SelfAssessments
            .FirstOrDefaultAsync(sa => sa.Id == id && sa.UserId == userId.Value);

        if (entity == null)
        {
            return NotFound();
        }

        entity.Mood = request.Mood;
        entity.Stress = request.Stress;
        entity.Workload = request.Workload;
        entity.Notes = request.Notes;

        await _dbContext.SaveChangesAsync();

        var response = new SelfAssessmentItemResponse(
            Id: entity.Id,
            CreatedAt: entity.CreatedAt,
            Mood: entity.Mood,
            Stress: entity.Stress,
            Workload: entity.Workload,
            Notes: entity.Notes
        );

        return Ok(response);
    }

    /// <summary>
    /// Remove uma autoavaliação do usuário autenticado.
    /// Ex.: DELETE /api/v1/selfassessments/{id}
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteSelfAssessment(Guid id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var entity = await _dbContext.SelfAssessments
            .FirstOrDefaultAsync(sa => sa.Id == id && sa.UserId == userId.Value);

        if (entity == null)
        {
            return NotFound();
        }

        _dbContext.SelfAssessments.Remove(entity);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Recupera o Id do usuário autenticado a partir do JWT.
    /// Usa o claim "sub" (JwtRegisteredClaimNames.Sub) definido em JwtTokenGenerator.
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }
}

// ----------------------------
// DTOs usados no controller
// ----------------------------

public record CreateSelfAssessmentRequest(
    MoodLevel Mood,
    StressLevel Stress,
    WorkloadLevel Workload,
    string? Notes
);

public record UpdateSelfAssessmentRequest(
    MoodLevel Mood,
    StressLevel Stress,
    WorkloadLevel Workload,
    string? Notes
);

public record SelfAssessmentItemResponse(
    Guid Id,
    DateTime CreatedAt,
    MoodLevel Mood,
    StressLevel Stress,
    WorkloadLevel Workload,
    string? Notes
);
