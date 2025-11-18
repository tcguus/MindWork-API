using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MindWork.Api.Domain.Enums;
using MindWork.Api.Infrastructure.Persistence;

namespace MindWork.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Roles = "Manager")] // só gestores podem ver o dashboard
public class DashboardController : ControllerBase
{
    private readonly MindWorkDbContext _dbContext;

    public DashboardController(MindWorkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Retorna um resumo anônimo das autoavaliações dos colaboradores
    /// nos últimos X dias (padrão: 30).
    /// Ex.: GET /api/v1/dashboard/summary?days=30
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryResponse>> GetSummary(
        [FromQuery] int days = 30)
    {
        if (days <= 0 || days > 365)
        {
            days = 30;
        }

        var sinceDate = DateTime.UtcNow.Date.AddDays(-days);

        var query = _dbContext.SelfAssessments
            .AsNoTracking()
            .Where(sa => sa.CreatedAt >= sinceDate);

        var totalAssessments = await query.CountAsync();
        if (totalAssessments == 0)
        {
            // Nenhuma avaliação no período → retornamos zeros
            var emptyResponse = new DashboardSummaryResponse(
                PeriodDays: days,
                TotalAssessments: 0,
                AverageMood: 0,
                AverageStress: 0,
                AverageWorkload: 0,
                MoodDistribution: new List<MoodDistributionItem>(),
                StressDistribution: new List<StressDistributionItem>(),
                WorkloadDistribution: new List<WorkloadDistributionItem>());

            return Ok(emptyResponse);
        }

        // Médias numéricas (simples, mapeando enums para int)
        var averageMood = await query.AverageAsync(sa => (int)sa.Mood);
        var averageStress = await query.AverageAsync(sa => (int)sa.Stress);
        var averageWorkload = await query.AverageAsync(sa => (int)sa.Workload);

        // Distribuições (contagem por nível)
        var moodGroups = await query
            .GroupBy(sa => sa.Mood)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync();

        var stressGroups = await query
            .GroupBy(sa => sa.Stress)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync();

        var workloadGroups = await query
            .GroupBy(sa => sa.Workload)
            .Select(g => new { Level = g.Key, Count = g.Count() })
            .ToListAsync();

        var moodDistribution = moodGroups
            .Select(g => new MoodDistributionItem(
                Level: g.Level,
                Count: g.Count))
            .ToList();

        var stressDistribution = stressGroups
            .Select(g => new StressDistributionItem(
                Level: g.Level,
                Count: g.Count))
            .ToList();

        var workloadDistribution = workloadGroups
            .Select(g => new WorkloadDistributionItem(
                Level: g.Level,
                Count: g.Count))
            .ToList();

        var response = new DashboardSummaryResponse(
            PeriodDays: days,
            TotalAssessments: totalAssessments,
            AverageMood: Math.Round(averageMood, 2),
            AverageStress: Math.Round(averageStress, 2),
            AverageWorkload: Math.Round(averageWorkload, 2),
            MoodDistribution: moodDistribution,
            StressDistribution: stressDistribution,
            WorkloadDistribution: workloadDistribution);

        return Ok(response);
    }
}

// ---------------------------------------
// DTOs de resposta para o dashboard
// ---------------------------------------

public record DashboardSummaryResponse(
    int PeriodDays,
    int TotalAssessments,
    double AverageMood,
    double AverageStress,
    double AverageWorkload,
    List<MoodDistributionItem> MoodDistribution,
    List<StressDistributionItem> StressDistribution,
    List<WorkloadDistributionItem> WorkloadDistribution
);

public record MoodDistributionItem(
    MoodLevel Level,
    int Count
);

public record StressDistributionItem(
    StressLevel Level,
    int Count
);

public record WorkloadDistributionItem(
    WorkloadLevel Level,
    int Count
);
