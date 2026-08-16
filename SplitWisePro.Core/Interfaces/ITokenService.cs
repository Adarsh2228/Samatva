using SplitWisePro.Core.DTOs;
using SplitWisePro.Core.Entities;

namespace SplitWisePro.Core.Interfaces;

/// <summary>
/// JWT token generation and validation service.
/// </summary>
public interface ITokenService
{
    /// <summary>Generate access + refresh tokens for a user.</summary>
    AuthResponse GenerateTokens(User user);

    /// <summary>Validate an expired access token and extract user claims.</summary>
    Guid? GetUserIdFromExpiredToken(string accessToken);

    /// <summary>Generate a guest link JWT for read-only group access.</summary>
    string GenerateGuestLinkToken(Guid groupId, int expirationDays);

    /// <summary>Validate a guest link token and extract the group ID.</summary>
    Guid? ValidateGuestLinkToken(string token);
}
