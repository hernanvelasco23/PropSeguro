using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PropSeguro.Domain.Users;

namespace PropSeguro.Api.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "PropSegura";
    public string Audience { get; set; } = "PropSegura";
    public string Secret { get; set; } = string.Empty;
    public string SigningKey { get; set; } = "dev-only-change-this-signing-key-for-propsegura-private-beta";
    public int ExpirationMinutes { get; set; } = 720;

    public string EffectiveSigningKey => string.IsNullOrWhiteSpace(Secret) ? SigningKey : Secret;
}

public interface IJwtTokenService
{
    string CreateToken(AppUser user);
}

public sealed class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public string CreateToken(AppUser user)
    {
        var options = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.EffectiveSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString("D")),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(options.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
