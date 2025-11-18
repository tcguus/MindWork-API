using Microsoft.EntityFrameworkCore;
using MindWork.Api.Domain.Entities;
using MindWork.Api.Domain.Enums;
using MindWork.Api.Infrastructure.Persistence;
using MindWork.Api.Services;
using Xunit;

namespace MindWork.Api.Tests;

public class AiServiceTests
{
    private MindWorkDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<MindWorkDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new MindWorkDbContext(options);
    }

    [Fact]
    public async Task GetPersonalizedRecommendationsAsync_ShouldReturnOnboarding_WhenUserHasNoAssessments()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new AiService(dbContext);
        var userId = Guid.NewGuid(); // usuário sem dados

        // Act
        var recommendations = await service.GetPersonalizedRecommendationsAsync(userId);

        // Assert
        Assert.NotEmpty(recommendations);
        Assert.Contains(recommendations, r => r.Category == "onboarding");
    }

    [Fact]
    public async Task GetPersonalizedRecommendationsAsync_ShouldIncludeStressManagement_WhenAverageStressIsHigh()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new AiService(dbContext);
        var userId = Guid.NewGuid();

        // cria algumas autoavaliações com estresse alto
        var assessments = new List<SelfAssessment>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                Mood = MoodLevel.Neutral,
                Stress = StressLevel.High,
                Workload = WorkloadLevel.Balanced,
                Notes = "Semana puxada."
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                Mood = MoodLevel.Good,
                Stress = StressLevel.VeryHigh,
                Workload = WorkloadLevel.High,
                Notes = "Muita demanda e prazos curtos."
            }
        };

        await dbContext.SelfAssessments.AddRangeAsync(assessments);
        await dbContext.SaveChangesAsync();

        // Act
        var recommendations = await service.GetPersonalizedRecommendationsAsync(userId);

        // Assert
        Assert.NotEmpty(recommendations);
        Assert.Contains(recommendations, r => r.Category == "stress_management");
    }

    [Fact]
    public async Task GetMonthlyReportAsync_ShouldReturnNoDataSummary_WhenThereAreNoAssessments()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new AiService(dbContext);
        var year = 2025;
        var month = 3;

        // Act
        var report = await service.GetMonthlyReportAsync(year, month);

        // Assert
        Assert.Equal(year, report.Year);
        Assert.Equal(month, report.Month);
        Assert.Equal(0, report.AverageMood);
        Assert.Equal(0, report.AverageStress);
        Assert.Equal(0, report.AverageWorkload);
        Assert.Contains("Nenhum dado de autoavaliação", report.Summary);
    }
}
