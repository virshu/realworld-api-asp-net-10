using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using RealWorld.Models.Entities;
using RealWorld.Services.Interface;
using Microsoft.IdentityModel.Tokens;
using RealWorld.Settings;
using Microsoft.Extensions.Options;

namespace RealWorld.Services;

class JwtService(IOptions<JwtSettingsOptions> jwtOptions) : IJwtService
{
    private readonly JwtSettingsOptions _jwtOptions = jwtOptions.Value;

    public string GenerateAccessToken(User user)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email)
        ];

        JwtSecurityToken token = new(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        byte[] randomNumber = new byte[64];
        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
    {
        TokenValidationParameters tokenValidationParameters = new()
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidAudience = _jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey)),
            ValidateLifetime = false // CRITICAL: We want to extract data even if it's expired
        };

        JwtSecurityTokenHandler tokenHandler = new();
        ClaimsPrincipal principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

        JwtSecurityToken? jwtSecurityToken = securityToken as JwtSecurityToken;
        if (jwtSecurityToken == null || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            throw new SecurityTokenException("Invalid token");

        return principal;
    }
}