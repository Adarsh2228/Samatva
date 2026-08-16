using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SplitWisePro.Core.DTOs;
using SplitWisePro.Core.Entities;
using SplitWisePro.Core.Interfaces;
using SplitWisePro.Infrastructure.Data;

namespace SplitWisePro.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext context, ITokenService tokenService,
        IEmailService emailService, ILogger<AuthController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _emailService = emailService;
        _logger = logger;
    }


    /// <summary>
    /// Register a new user account.
    /// POST /api/auth/register
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        _logger.LogInformation("=== REGISTER ATTEMPT: {Email} from Origin: {Origin} ===",
            request?.Email, Request.Headers.Origin.ToString());

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Register validation failed for {Email}: {Errors}",
                request?.Email, string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return BadRequest(ModelState);
        }

        try
        {
            // Check if email already exists
            _logger.LogInformation("Checking if email exists: {Email}", request.Email);
            var existingUser = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);

            if (existingUser)
            {
                _logger.LogWarning("Email already exists: {Email}", request.Email);
                return Conflict(new { message = "A user with this email already exists." });
            }

            _logger.LogInformation("Creating new user: {Email}", request.Email);
            var user = new User
            {
                Id = Guid.NewGuid(),
                DisplayName = request.DisplayName,
                Email = request.Email.ToLower().Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
                PhoneNumber = request.PhoneNumber,
                UpiId = request.UpiId,
                DefaultCurrency = request.DefaultCurrency
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("User saved to DB: {Email} / {Id}", user.Email, user.Id);

            var authResponse = _tokenService.GenerateTokens(user);
            _logger.LogInformation("Tokens generated for: {Email}", user.Email);

            // Store hashed refresh token
            user.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(authResponse.RefreshToken);
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("=== REGISTER SUCCESS: {Email} ===", user.Email);
            return CreatedAtAction(nameof(GetProfile), null, authResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== REGISTER ERROR for {Email}: {Message} ===", request?.Email, ex.Message);
            return StatusCode(500, new { message = $"Registration error: {ex.Message}" });
        }
    }

    /// <summary>
    /// Login with email and password.
    /// POST /api/auth/login
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower(), ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        var authResponse = _tokenService.GenerateTokens(user);

        // Store hashed refresh token
        user.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(authResponse.RefreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User logged in: {Email}", user.Email);

        return Ok(authResponse);
    }

    /// <summary>
    /// Refresh an expired access token using a valid refresh token.
    /// POST /api/auth/refresh
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var userId = _tokenService.GetUserIdFromExpiredToken(request.AccessToken);
        if (userId is null)
            return Unauthorized(new { message = "Invalid access token." });

        var user = await _context.Users.FindAsync(new object[] { userId.Value }, ct);
        if (user is null ||
            user.RefreshTokenExpiryTime < DateTime.UtcNow ||
            user.RefreshTokenHash is null ||
            !BCrypt.Net.BCrypt.Verify(request.RefreshToken, user.RefreshTokenHash))
        {
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        var authResponse = _tokenService.GenerateTokens(user);

        // Rotate refresh token
        user.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(authResponse.RefreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _context.SaveChangesAsync(ct);

        return Ok(authResponse);
    }

    /// <summary>
    /// Logout and invalidate the refresh token.
    /// POST /api/auth/logout
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var user = await _context.Users.FindAsync(new object[] { userId.Value }, ct);
        if (user is not null)
        {
            user.RefreshTokenHash = null;
            user.RefreshTokenExpiryTime = null;
            await _context.SaveChangesAsync(ct);
        }

        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>
    /// Get current user's profile.
    /// GET /api/auth/profile
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var user = await _context.Users.FindAsync(new object[] { userId.Value }, ct);
        if (user is null) return NotFound();

        return Ok(new UserDto
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            UpiId = user.UpiId,
            DefaultCurrency = user.DefaultCurrency,
            TimeZone = user.TimeZone
        });
    }

    // ── Helper ─────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/auth/forgot-password
    /// Generates 5-digit OTP and sends email. Always returns 200 to prevent email enumeration.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == req.Email.ToLower(), ct);

        // Always return success to prevent email enumeration
        if (user is null)
            return Ok(new { message = "If this email is registered, an OTP has been sent." });

        // Generate secure 5-digit OTP
        var otp = Random.Shared.Next(10000, 99999).ToString();

        // Hash and store OTP
        user.PasswordResetOtpHash = BCrypt.Net.BCrypt.HashPassword(otp, workFactor: 10);
        user.PasswordResetOtpExpiry = DateTime.UtcNow.AddMinutes(10);
        user.OtpAttempts = 0;
        await _context.SaveChangesAsync(ct);

        // Send email (non-blocking on error)
        await _emailService.SendOtpEmailAsync(user.Email, user.DisplayName, otp, ct);

        _logger.LogInformation("OTP generated for {Email}", user.Email);
        return Ok(new { message = "If this email is registered, an OTP has been sent." });
    }

    /// <summary>
    /// POST /api/auth/verify-otp
    /// Verifies the 5-digit OTP without changing the password (used for frontend flow).
    /// </summary>
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == req.Email.ToLower(), ct);

        if (user is null || user.PasswordResetOtpHash is null || user.PasswordResetOtpExpiry < DateTime.UtcNow)
            return BadRequest(new { message = "OTP is invalid or has expired. Please request a new one." });

        if (user.OtpAttempts >= 5)
            return BadRequest(new { message = "Too many attempts. Please request a new OTP." });

        if (!BCrypt.Net.BCrypt.Verify(req.Otp, user.PasswordResetOtpHash))
        {
            user.OtpAttempts++;
            await _context.SaveChangesAsync(ct);
            var remaining = 5 - user.OtpAttempts;
            return BadRequest(new { message = $"Invalid OTP. {remaining} attempt(s) remaining." });
        }

        return Ok(new { message = "OTP verified successfully.", verified = true });
    }

    /// <summary>
    /// POST /api/auth/reset-password
    /// Verifies OTP + sets new password + auto-logins the user.
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == req.Email.ToLower(), ct);

        if (user is null || user.PasswordResetOtpHash is null || user.PasswordResetOtpExpiry < DateTime.UtcNow)
            return BadRequest(new { message = "OTP is invalid or has expired." });

        if (user.OtpAttempts >= 5)
            return BadRequest(new { message = "Too many attempts. Please request a new OTP." });

        if (!BCrypt.Net.BCrypt.Verify(req.Otp, user.PasswordResetOtpHash))
        {
            user.OtpAttempts++;
            await _context.SaveChangesAsync(ct);
            return BadRequest(new { message = "Invalid OTP." });
        }

        // Update password and clear OTP
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword, workFactor: 12);
        user.PasswordResetOtpHash = null;
        user.PasswordResetOtpExpiry = null;
        user.OtpAttempts = 0;

        // Auto-login: generate new tokens
        var authResponse = _tokenService.GenerateTokens(user);
        user.RefreshTokenHash = BCrypt.Net.BCrypt.HashPassword(authResponse.RefreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _context.SaveChangesAsync(ct);

        // Send confirmation email
        _ = _emailService.SendPasswordChangedEmailAsync(user.Email, user.DisplayName);

        _logger.LogInformation("Password reset successful for {Email}", user.Email);
        return Ok(authResponse); // Return auth tokens so user is auto-logged in
    }

    // ── Helper ─────────────────────────────────────────────────────────

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim is not null ? Guid.Parse(claim) : null;
    }
}
