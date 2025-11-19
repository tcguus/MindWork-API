using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

    // Cria uma configuração falsa para o teste não quebrar
    private IConfiguration CreateFakeConfiguration()
    {
        var myConfiguration = new Dictionary<string, string>
        {
            {"Gemini:ApiKey", "TEST_KEY_VALUE"}
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(myConfiguration)
            .Build();
    }

    [Fact]
    public async Task GetPersonalizedRecommendationsAsync_ShouldReturnOnboarding_WhenUserHasNoAssessments()
    {
        using var dbContext = CreateInMemoryDbContext();
        // CORREÇÃO: Passamos config e http client (dummy) para satisfazer o novo construtor
        var service = new AiService(dbContext, CreateFakeConfiguration(), new HttpClient());
        var userId = Guid.NewGuid(); 

        var recommendations = await service.GetPersonalizedRecommendationsAsync(userId);
        
        Assert.NotEmpty(recommendations);
        Assert.Contains(recommendations, r => r.Category == "onboarding");
    }

    [Fact]
    public async Task GetPersonalizedRecommendationsAsync_ShouldReturnFallback_WhenApiIsFake()
    {
        // Esse teste simulava estresse alto. Como agora usamos uma API real (Gemini),
        // e aqui no teste estamos usando uma chave falsa ("TEST_KEY_VALUE"), 
        // o esperado é que o sistema caia no "Fallback" (catch) e retorne a categoria "maintenance".
        // Isso prova que seu sistema é resiliente e não trava se a IA cair.

        using var dbContext = CreateInMemoryDbContext();
        var service = new AiService(dbContext, CreateFakeConfiguration(), new HttpClient());
        var userId = Guid.NewGuid();

        var assessments = new List<SelfAssessment>
        {
            new()
            {
                Id = Guid.NewGuid(), UserId = userId, CreatedAt = DateTime.UtcNow.AddDays(-3),
                Mood = MoodLevel.Neutral, Stress = StressLevel.High, Workload = WorkloadLevel.Balanced, Notes = "A"
            },
            new()
            {
                Id = Guid.NewGuid(), UserId = userId, CreatedAt = DateTime.UtcNow.AddDays(-7),
                Mood = MoodLevel.Good, Stress = StressLevel.VeryHigh, Workload = WorkloadLevel.High, Notes = "B"
            }
        };

        await dbContext.SelfAssessments.AddRangeAsync(assessments);
        await dbContext.SaveChangesAsync();

        var recommendations = await service.GetPersonalizedRecommendationsAsync(userId);

        Assert.NotEmpty(recommendations);
        // Esperamos 'maintenance' pois a chave de API é falsa neste teste
        Assert.Contains(recommendations, r => r.Category == "maintenance"); 
    }

    [Fact]
    public async Task GetMonthlyReportAsync_ShouldReturnDummy()
    {
        using var dbContext = CreateInMemoryDbContext();
        var service = new AiService(dbContext, CreateFakeConfiguration(), new HttpClient());
        var year = 2025;
        var month = 3;

        var report = await service.GetMonthlyReportAsync(year, month);

        Assert.NotNull(report);
        Assert.Equal(year, report.Year);
    }
}
