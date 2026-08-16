using System.ComponentModel.DataAnnotations;

namespace SplitWisePro.Core.DTOs;

// ── Registration ───────────────────────────────────────────────────

public class RegisterRequest
{
    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(100)]
    public string? UpiId { get; set; }

    [MaxLength(3)]
    public string DefaultCurrency { get; set; } = "INR";
}

// ── Login ──────────────────────────────────────────────────────────

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

// ── Token Response ─────────────────────────────────────────────────

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiry { get; set; }
    public UserDto User { get; set; } = null!;
}

// ── Refresh Token ──────────────────────────────────────────────────

public class RefreshTokenRequest
{
    [Required]
    public string AccessToken { get; set; } = string.Empty;

    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

// ── User DTO ───────────────────────────────────────────────────────

public class UserDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? UpiId { get; set; }
    public string DefaultCurrency { get; set; } = "INR";
    public string TimeZone { get; set; } = "Asia/Kolkata";
}

// ── Password Reset OTP ─────────────────────────────────────────────

public class ForgotPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class VerifyOtpRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(5, MinimumLength = 5)]
    public string Otp { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(5, MinimumLength = 5)]
    public string Otp { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}

