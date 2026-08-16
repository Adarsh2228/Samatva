namespace SplitWisePro.Core.Interfaces;

/// <summary>
/// Sends transactional emails (OTP, notifications).
/// </summary>
public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string toName, string otp, CancellationToken ct = default);
    Task SendPasswordChangedEmailAsync(string toEmail, string toName, CancellationToken ct = default);
}
