using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SplitWisePro.Core.DTOs;
using SplitWisePro.Core.Entities;
using SplitWisePro.Core.Interfaces;

namespace SplitWisePro.Infrastructure.Services;

/// <summary>
/// JWT token generation and validation using HMAC-SHA256.
/// Handles access tokens, refresh tokens, and guest link tokens.
/// </summary>
public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly SymmetricSecurityKey _signingKey;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
        var secretKey = _configuration["JwtSettings:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured.");
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    }

    public AuthResponse GenerateTokens(User user)
    {
        var accessTokenExpiry = DateTime.UtcNow.AddMinutes(
            int.Parse(_configuration["JwtSettings:AccessTokenExpirationMinutes"] ?? "30"));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new("currency", user.DefaultCurrency),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var accessToken = GenerateJwtToken(claims, accessTokenExpiry);
        var refreshToken = GenerateRefreshToken();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiry = accessTokenExpiry,
            User = new UserDto
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                AvatarUrl = user.AvatarUrl,
                UpiId = user.UpiId,
                DefaultCurrency = user.DefaultCurrency,
                TimeZone = user.TimeZone
            }
        };
    }

    public Guid? GetUserIdFromExpiredToken(string accessToken)
    {
        var tokenValidationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false, // Allow expired tokens for refresh
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["JwtSettings:Issuer"],
            ValidAudience = _configuration["JwtSettings:Audience"],
            IssuerSigningKey = _signingKey
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(accessToken, tokenValidationParams, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim is not null ? Guid.Parse(userIdClaim) : null;
        }
        catch
        {
            return null;
        }
    }

    public string GenerateGuestLinkToken(Guid groupId, int expirationDays)
    {
        var claims = new List<Claim>
        {
            new("groupId", groupId.ToString()),
            new("tokenType", "guest_link"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var expiry = DateTime.UtcNow.AddDays(expirationDays);
        return GenerateJwtToken(claims, expiry);
    }

    public Guid? ValidateGuestLinkToken(string token)
    {
        var tokenValidationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _configuration["JwtSettings:Issuer"],
            ValidAudience = _configuration["JwtSettings:Audience"],
            IssuerSigningKey = _signingKey
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, tokenValidationParams, out _);

            var tokenType = principal.FindFirst("tokenType")?.Value;
            if (tokenType != "guest_link") return null;

            var groupIdClaim = principal.FindFirst("groupId")?.Value;
            return groupIdClaim is not null ? Guid.Parse(groupIdClaim) : null;
        }
        catch
        {
            return null;
        }
    }

    // ── Private Helpers ────────────────────────────────────────────────

    private string GenerateJwtToken(IEnumerable<Claim> claims, DateTime expiry)
    {
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["JwtSettings:Issuer"],
            audience: _configuration["JwtSettings:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
