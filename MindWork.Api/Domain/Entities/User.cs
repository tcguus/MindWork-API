namespace MindWork.Api.Domain.Entities;

public enum UserRole
{
  Collaborator = 1,
  Manager = 2
}

public class User
{
  public Guid Id { get; set; }

  public string FullName { get; set; } = default!;

  public string Email { get; set; } = default!;

  /// <summary>
  /// Senha já armazenada em formato de hash.
  /// Nunca guardar senha em texto puro.
  /// </summary>
  public string PasswordHash { get; set; } = default!;

  public UserRole Role { get; set; }

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  public bool IsActive { get; set; } = true;
}
