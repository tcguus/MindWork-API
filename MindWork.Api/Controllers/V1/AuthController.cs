using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MindWork.Api.Domain.Entities;
using MindWork.Api.Domain.Enums;
using MindWork.Api.Infrastructure.Authentication;
using MindWork.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MindWork.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class AuthController : ControllerBase
{
    private readonly MindWorkDbContext _dbContext;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthController(MindWorkDbContext dbContext, IJwtTokenGenerator jwtTokenGenerator)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    /// <summary>
    /// Registro de novo usuário (colaborador ou gestor).
    /// Usado pelo app Mobile para cadastro.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        // validações básicas
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
        {
            return BadRequest("Role inválida. Use 'Collaborator' ou 'Manager'.");
        }

        var existingUser = _dbContext.Users.FirstOrDefault(u => u.Email == request.Email);
        if (existingUser != null)
        {
            return Conflict("Já existe um usuário cadastrado com esse e-mail.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = passwordHash,
            Role = role,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();

        var token = _jwtTokenGenerator.GenerateToken(user);

        var response = new AuthResponse(
            Token: token,
            FullName: user.FullName,
            Role: user.Role.ToString());

        return Ok(response);
    }

    /// <summary>
    /// Login com e-mail e senha.
    /// Retorna um JWT para acesso às rotas protegidas.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _dbContext.Users
            .Where(u => u.Email == request.Email && u.IsActive)
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return Unauthorized("Credenciais inválidas.");
        }

        var passwordOk = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!passwordOk)
        {
            return Unauthorized("Credenciais inválidas.");
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        var response = new AuthResponse(
            Token: token,
            FullName: user.FullName,
            Role: user.Role.ToString());

        return Ok(response);
    }
}

// ----------------------------
// DTOs usados no AuthController
// ----------------------------

public record RegisterRequest(
    string FullName,
    string Email,
    string Password,
    string Role // "Collaborator" ou "Manager"
);

public record LoginRequest(
    string Email,
    string Password
);

public record AuthResponse(
    string Token,
    string FullName,
    string Role
);
