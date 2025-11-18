using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MindWork.Api.Services;

namespace MindWork.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;

    public AiController(IAiService aiService)
    {
        _aiService = aiService;
    }

    /// <summary>
    /// Recomendações personalizadas para o usuário autenticado,
    /// com base nas autoavaliações recentes.
    /// Ex.: GET /api/v1/ai/recommendations/me
    /// </summary>
    [HttpGet("recommendations/me")]
    [Authorize] // precisa estar logado
    public async Task<ActionResult<List<AiRecommendation>>> GetMyRecommendations()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var recommendations = await _aiService.GetPersonalizedRecommendationsAsync(userId.Value);
        return Ok(recommendations);
    }

    /// <summary>
    /// Relatório mensal do clima emocional da empresa (ou time),
    /// baseado nas autoavaliações do período.
    /// Ex.: GET /api/v1/ai/monthly-report?year=2025&month=3
    /// </summary>
    [HttpGet("monthly-report")]
    [Authorize(Roles = "Manager")] // apenas gestores
    public async Task<ActionResult<MonthlyReportResult>> GetMonthlyReport(
        [FromQuery] int? year = null,
        [FromQuery] int? month = null)
    {
        var now = DateTime.UtcNow;

        var reportYear = year ?? now.Year;
        var reportMonth = month ?? now.Month;

        // validações simples
        if (reportMonth < 1 || reportMonth > 12)
        {
            return BadRequest("O mês deve estar entre 1 e 12.");
        }

        var result = await _aiService.GetMonthlyReportAsync(reportYear, reportMonth);
        return Ok(result);
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
