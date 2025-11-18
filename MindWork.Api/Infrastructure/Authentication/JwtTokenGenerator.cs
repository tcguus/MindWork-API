using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MindWork.Api.Domain.Entities;

namespace MindWork.Api.Infrastructure.Authentication;

public interface IJwtTokenGenerator
{
  string GenerateToken(User user);
}

public class JwtTokenGenerator : IJwtTokenGenerator
{
  private readonly IConfiguration _configuration;

  public JwtTokenGenerator(IConfiguration configuration)
  {
    _configuration = configuration;
  }

  public string GenerateToken(User user)
  {
    var issuer = _configuration["Jwt:Issuer"]
                 ?? throw new InvalidOperationException("Jwt:Issuer not configured.");
    var audience = _configuration["Jwt:Audience"]
                   ?? throw new InvalidOperationException("Jwt:Audience not configured.");
    var secretKey = _configuration["Jwt:SecretKey"]
                    ?? throw new InvalidOperationException("Jwt:SecretKey not configured.");

    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

    var claims = new List<Claim>
    {
      new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
      new(JwtRegisteredClaimNames.Email, user.Email),
      new("fullName", user.FullName),
      new(ClaimTypes.Role, user.Role.ToString())
    };

    var token = new JwtSecurityToken(
      issuer: issuer,
      audience: audience,
      claims: claims,
      notBefore: DateTime.UtcNow,
      expires: DateTime.UtcNow.AddHours(8),
      signingCredentials: credentials
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
  }
}
