using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Services;

public class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public static TimeSpan GetAccessTokenLifetime(IConfiguration config)
    {
        if (!double.TryParse(config["Jwt:ExpirationHours"], out var hours) ||
            hours <= 0 || hours > 24)
        {
            throw new InvalidOperationException("Jwt:ExpirationHours deve estar entre 0 e 24 horas.");
        }

        return TimeSpan.FromHours(hours);
    }

    public string GenerateToken(User user)
    {
        var secret = _config["Jwt:Secret"];

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("Jwt:Secret não foi configurado.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.ID.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.ID.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.Nome),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var lifetime = GetAccessTokenLifetime(_config);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public int GetAccessTokenLifetimeSeconds()
    {
        return (int)GetAccessTokenLifetime(_config).TotalSeconds;
    }
}
