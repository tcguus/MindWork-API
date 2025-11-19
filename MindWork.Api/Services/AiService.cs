using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MindWork.Api.Domain.Enums;
using MindWork.Api.Infrastructure.Persistence;

namespace MindWork.Api.Services;

public interface IAiService
{
    Task<List<AiRecommendation>> GetPersonalizedRecommendationsAsync(Guid userId);
    Task<MonthlyReportResult> GetMonthlyReportAsync(int year, int month);
}

public class AiService : IAiService
{
    private readonly MindWorkDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public AiService(MindWorkDbContext dbContext, IConfiguration configuration, HttpClient httpClient)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task<List<AiRecommendation>> GetPersonalizedRecommendationsAsync(Guid userId)
    {
        // 1. Validação da chave
        var apiKey = _configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new List<AiRecommendation> { new("ERRO DE CONFIG", "Chave API não encontrada.", "debug_error") };
        }

        // 2. Coletar dados do banco
        var sinceDate = DateTime.UtcNow.Date.AddDays(-30);
        var assessments = await _dbContext.SelfAssessments
            .AsNoTracking()
            .Where(sa => sa.UserId == userId && sa.CreatedAt >= sinceDate)
            .OrderByDescending(sa => sa.CreatedAt)
            .Take(5)
            .ToListAsync();

        if (!assessments.Any())
        {
            return new List<AiRecommendation> { new("Sem dados", "Registre seu humor primeiro.", "onboarding") };
        }

        // 3. Montar o Prompt
        var sb = new StringBuilder();
        sb.AppendLine("Atue como um psicólogo corporativo.");
        sb.AppendLine("Analise estes dados (1 a 5):");
        foreach (var a in assessments)
        {
            sb.AppendLine($"- Humor: {a.Mood}, Estresse: {a.Stress}, Notas: {a.Notes}");
        }
        sb.AppendLine("\nGere 3 recomendações curtas.");
        sb.AppendLine("Responda APENAS um JSON array: [{ \"Title\": \"...\", \"Description\": \"...\", \"Category\": \"...\" }]");

        // 4. Chamar Gemini (ATUALIZADO PARA 2025: gemini-2.5-flash)
        // Se o 2.5 falhar, a única alternativa restante seria o gemini-2.0-flash
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
        
        var payload = new { contents = new[] { new { parts = new[] { new { text = sb.ToString() } } } } };
        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, jsonContent);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return new List<AiRecommendation> { 
                    new("ERRO GOOGLE", $"Status: {response.StatusCode}. Detalhe: {errorBody}", "debug_error") 
                };
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var geminiResponse = JsonSerializer.Deserialize<GeminiResponseRoot>(responseString);
            var rawText = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(rawText))
            {
                 return new List<AiRecommendation> { new("IA SEM TEXTO", "O Google respondeu vazio.", "debug_error") };
            }

            // Limpeza do Markdown
            rawText = rawText.Replace("```json", "").Replace("```", "").Trim();
            
            try 
            {
                var recommendations = JsonSerializer.Deserialize<List<AiRecommendation>>(rawText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (recommendations != null) return recommendations;
            }
            catch
            {
                return new List<AiRecommendation> { new("Recomendação (Texto Cru)", rawText, "general_advice") };
            }
        }
        catch (Exception ex)
        {
            return new List<AiRecommendation> { new("ERRO CONEXÃO", ex.Message, "debug_error") };
        }

        return new List<AiRecommendation> { new("ERRO DESCONHECIDO", "Falha inesperada.", "debug_error") };
    }

    public async Task<MonthlyReportResult> GetMonthlyReportAsync(int year, int month)
    {
        return new MonthlyReportResult(year, month, "Relatório indisponível", 0,0,0, new(), new());
    }
}

// Classes auxiliares
public class GeminiResponseRoot { [JsonPropertyName("candidates")] public List<Candidate>? Candidates { get; set; } }
public class Candidate { [JsonPropertyName("content")] public Content? Content { get; set; } }
public class Content { [JsonPropertyName("parts")] public List<Part>? Parts { get; set; } }
public class Part { [JsonPropertyName("text")] public string? Text { get; set; } }

public record AiRecommendation(string Title, string Description, string Category);
public record MonthlyReportResult(int Year, int Month, string Summary, double AverageMood, double AverageStress, double AverageWorkload, List<string> KeyFindings, List<string> SuggestedActions);
