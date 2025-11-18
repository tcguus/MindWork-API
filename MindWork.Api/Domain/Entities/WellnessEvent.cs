namespace MindWork.Api.Domain.Entities;

/// <summary>
/// Representa um evento relacionado ao bem-estar do colaborador,
/// que pode vir de dispositivos IoT, integrações externas ou do próprio app.
/// Ex.: pausa realizada, sessão de mindfulness concluída,
/// detecção de alto tempo de tela, ergonomia ruim etc.
/// </summary>
public class WellnessEvent
{
  public Guid Id { get; set; }

  /// <summary>
  /// Usuário associado ao evento (pode ser opcional se o evento for só de ambiente).
  /// </summary>
  public Guid? UserId { get; set; }

  /// <summary>
  /// Tipo do evento (ex.: "break", "focus_session", "high_stress_signal").
  /// Vamos manter string para permitir tipos flexíveis configurados pela solução de IoT/IA.
  /// </summary>
  public string EventType { get; set; } = default!;

  /// <summary>
  /// Data/hora em que o evento ocorreu (UTC).
  /// </summary>
  public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

  /// <summary>
  /// Origem do evento (ex.: "mobile_app", "wearable", "desk_sensor").
  /// Ajuda a IoT/IA a entender de onde estão vindo os dados.
  /// </summary>
  public string Source { get; set; } = "unknown";

  /// <summary>
  /// Intensidade ou valor numérico associado ao evento (opcional).
  /// Ex.: nível de movimento, tempo de pausa em minutos, batimentos cardíacos médios.
  /// </summary>
  public double? Value { get; set; }

  /// <summary>
  /// JSON livre com dados adicionais para a solução de IA interpretar.
  /// Ex.: { "heartRate": 85, "roomNoiseLevel": 70 }
  /// </summary>
  public string? MetadataJson { get; set; }
}
