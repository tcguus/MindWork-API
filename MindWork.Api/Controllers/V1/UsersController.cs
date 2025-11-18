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
[Authorize]
public class UsersController : ControllerBase
{
    private readonly MindWorkDbContext _dbContext;

    public UsersController(MindWorkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Retorna os dados básicos do usuário autenticado.
    /// Ex.: GET /api/v1/users/me
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileResponse>> GetMe()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
        {
            return NotFound();
        }

        var response = new UserProfileResponse(
            Id: user.Id,
            FullName: user.FullName,
            Email: user.Email,
            Role: user.Role.ToString(),
            CreatedAt: user.CreatedAt,
            IsActive: user.IsActive
        );

        return Ok(response);
    }

    /// <summary>
    /// Lista usuários com paginação (apenas gestores).
    /// Ex.: GET /api/v1/users?pageNumber=1&pageSize=10&role=Collaborator
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Manager")]
    public async Task<ActionResult<PagedResponse<UserListItemResponse>>> GetUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? role = null,
        [FromQuery] bool? isActive = null)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0 || pageSize > 50) pageSize = 10;

        var query = _dbContext.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(role) &&
            Enum.TryParse<UserRole>(role, true, out var parsedRole))
        {
            query = query.Where(u => u.Role == parsedRole);
        }

        if (isActive != null)
        {
            query = query.Where(u => u.IsActive == isActive);
        }

        query = query.OrderBy(u => u.FullName);

        var totalCount = await query.CountAsync();

        var users = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = users.Select(u => new UserListItemResponse(
            Id: u.Id,
            FullName: u.FullName,
            Email: u.Email,
            Role: u.Role.ToString(),
            IsActive: u.IsActive
        ));

        var pagedResponse = new PagedResponse<UserListItemResponse>(
            items: items,
            pageNumber: pageNumber,
            pageSize: pageSize,
            totalCount: totalCount);

        // HATEOAS links
        var selfUrl = Url.ActionLink(
            action: nameof(GetUsers),
            controller: "Users",
            values: new { version = "1", pageNumber, pageSize, role, isActive });

        if (selfUrl != null)
        {
            pagedResponse.AddLink(selfUrl, "self", "GET");
        }

        if (pagedResponse.HasNext)
        {
            var nextUrl = Url.ActionLink(
                action: nameof(GetUsers),
                controller: "Users",
                values: new { version = "1", pageNumber = pageNumber + 1, pageSize, role, isActive });

            if (nextUrl != null)
            {
                pagedResponse.AddLink(nextUrl, "next", "GET");
            }
        }

        if (pagedResponse.HasPrevious)
        {
            var prevUrl = Url.ActionLink(
                action: nameof(GetUsers),
                controller: "Users",
                values: new { version = "1", pageNumber = pageNumber - 1, pageSize, role, isActive });

            if (prevUrl != null)
            {
                pagedResponse.AddLink(prevUrl, "previous", "GET");
            }
        }

        return Ok(pagedResponse);
    }

    /// <summary>
    /// Ativa ou desativa um usuário (apenas gestores).
    /// Ex.: PUT /api/v1/users/{id}/status
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest request)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return NotFound();
        }

        user.IsActive = request.IsActive;
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

// ----------------------------
// DTOs de resposta/entrada
// ----------------------------

public record UserProfileResponse(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    DateTime CreatedAt,
    bool IsActive
);

public record UserListItemResponse(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    bool IsActive
);

public record UpdateUserStatusRequest(
    bool IsActive
);
