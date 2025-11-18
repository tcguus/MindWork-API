using System.Net;
using System.Text.Json;

namespace MindWork.Api.Middleware;

/// <summary>
/// Middleware global de tratamento de erros.
/// Garante que qualquer exceção não tratada seja:
/// - logada;
/// - retornada como JSON padronizado (Problem Details-like),
/// em vez de HTML feio / stack trace.
/// Ajuda nos requisitos de observabilidade e robustez da API.
/// </summary>
public class ErrorHandlingMiddleware
{
  private readonly RequestDelegate _next;
  private readonly ILogger<ErrorHandlingMiddleware> _logger;

  public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
  {
    _next = next;
    _logger = logger;
  }

  public async Task InvokeAsync(HttpContext context)
  {
    try
    {
      await _next(context);
    }
    catch (Exception ex)
    {
      var traceId = Guid.NewGuid().ToString();

      _logger.LogError(
        ex,
        "Unhandled exception. TraceId={TraceId}, Path={Path}, Method={Method}",
        traceId,
        context.Request.Path,
        context.Request.Method);

      context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
      context.Response.ContentType = "application/json";

      var problem = new
      {
        type = "https://httpstatuses.io/500",
        title = "Erro interno no servidor",
        status = context.Response.StatusCode,
        traceId,
        detail = "Ocorreu um erro inesperado ao processar sua requisição. " +
                 "Se o problema persistir, entre em contato com o suporte."
      };

      var json = JsonSerializer.Serialize(problem);
      await context.Response.WriteAsync(json);
    }
  }
}
