using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindWork.Api.Domain.Entities;
using MindWork.Api.Infrastructure.Persistence;
using MindWork.Api.Models.Responses;

namespace MindWork.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize] // exige JWT para registrar / consultar eventos
public class WellnessEventsController : ControllerBase
{
    private readonly MindWorkDbContext _dbContext;

    public WellnessEventsController(MindWorkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Cria um novo evento de bem-estar.
    /// Pode ser chamado pelo app mobile ou por integrações IoT/externas.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<WellnessEventItemResponse>> Create(
        [FromBody] CreateWellnessEventRequest request)
    {
        Guid? userId = request.UserId;

        // Se não veio UserId no corpo, tentamos usar o usuário autenticado
        if (userId == null)
        {
            userId = GetCurrentUserId();
        }

        var entity = new WellnessEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = request.EventType,
            OccurredAt = request.OccurredAt ?? DateTime.UtcNow,
            Source = string.IsNullOrWhiteSpace(request.Source) ? "unknown" : request.Source,
            Value = request.Value,
            MetadataJson = request.MetadataJson
        };

        await _dbContext.WellnessEvents.AddAsync(entity);
        await _dbContext.SaveChangesAsync();

        var response = new WellnessEventItemResponse(
            Id: entity.Id,
            UserId: entity.UserId,
            EventType: entity.EventType,
            OccurredAt: entity.OccurredAt,
            Source: entity.Source,
            Value: entity.Value,
            MetadataJson: entity.MetadataJson
        );

        return CreatedAtAction(
            nameof(Get),
            new { version = "1", pageNumber = 1, pageSize = 10 },
            response);
    }

    /// <summary>
    /// Lista eventos de bem-estar com paginação e filtros opcionais.
    /// Exemplo:
    /// GET /api/v1/wellnessevents?pageNumber=1&pageSize=10&eventType=break&source=wearable
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Manager")] // somo gestores por padrão; podemos flexibilizar depois
    public async Task<ActionResult<PagedResponse<WellnessEventItemResponse>>> Get(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? eventType = null,
        [FromQuery] string? source = null,
        [FromQuery] DateTime? occurredFrom = null,
        [FromQuery] DateTime? occurredTo = null)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0 || pageSize > 50) pageSize = 10;

        var query = _dbContext.WellnessEvents
            .AsNoTracking()
            .AsQueryable();

        if (userId != null)
        {
            query = query.Where(e => e.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(e => e.Source == source);
        }

        if (occurredFrom != null)
        {
            query = query.Where(e => e.OccurredAt >= occurredFrom);
        }

        if (occurredTo != null)
        {
            query = query.Where(e => e.OccurredAt <= occurredTo);
        }

        query = query.OrderByDescending(e => e.OccurredAt);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var itemResponses = items.Select(e => new WellnessEventItemResponse(
            Id: e.Id,
            UserId: e.UserId,
            EventType: e.EventType,
            OccurredAt: e.OccurredAt,
            Source: e.Source,
            Value: e.Value,
            MetadataJson: e.MetadataJson
        ));

        var pagedResponse = new PagedResponse<WellnessEventItemResponse>(
            items: itemResponses,
            pageNumber: pageNumber,
            pageSize: pageSize,
            totalCount: totalCount);

        // HATEOAS links
        var selfUrl = Url.ActionLink(
            action: nameof(Get),
            controller: "WellnessEvents",
            values: new
            {
                version = "1",
                pageNumber,
                pageSize,
                userId,
                eventType,
                source,
                occurredFrom,
                occurredTo
            });

        if (selfUrl != null)
        {
            pagedResponse.AddLink(selfUrl, "self", "GET");
        }

        if (pagedResponse.HasNext)
        {
            var nextUrl = Url.ActionLink(
                action: nameof(Get),
                controller: "WellnessEvents",
                values: new
                {
                    version = "1",
                    pageNumber = pageNumber + 1,
                    pageSize,
                    userId,
                    eventType,
                    source,
                    occurredFrom,
                    occurredTo
                });

            if (nextUrl != null)
            {
                pagedResponse.AddLink(nextUrl, "next", "GET");
            }
        }

        if (pagedResponse.HasPrevious)
        {
            var prevUrl = Url.ActionLink(
                action: nameof(Get),
                controller: "WellnessEvents",
                values: new
                {
                    version = "1",
                    pageNumber = pageNumber - 1,
                    pageSize,
                    userId,
                    eventType,
                    source,
                    occurredFrom,
                    occurredTo
                });

            if (prevUrl != null)
            {
                pagedResponse.AddLink(prevUrl, "previous", "GET");
            }
        }

        return Ok(pagedResponse);
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

public record CreateWellnessEventRequest(
    Guid? UserId,
    string EventType,
    DateTime? OccurredAt,
    string? Source,
    double? Value,
    string? MetadataJson
);

public record WellnessEventItemResponse(
    Guid Id,
    Guid? UserId,
    string EventType,
    DateTime OccurredAt,
    string Source,
    double? Value,
    string? MetadataJson
);
