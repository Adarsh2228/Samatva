using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using SplitWisePro.Core.Interfaces;

namespace SplitWisePro.Infrastructure.Services;

/// <summary>
/// Sends emails via SMTP (Brevo free tier — 300 emails/day).
/// Configure in appsettings / environment variables:
///   Email__SmtpHost, Email__SmtpPort, Email__From, Email__Username, Email__Password, Email__FromName
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendOtpEmailAsync(string toEmail, string toName, string otp, CancellationToken ct = default)
    {
        var subject = "🔐 Your SplitWise Pro Password Reset OTP";
        var body = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; background:#0f0e17; color:#fffffe; margin:0; padding:0;'>
  <div style='max-width:520px; margin:40px auto; background:#1a1a2e; border-radius:20px; overflow:hidden; border:1px solid rgba(108,92,231,0.3);'>
    <div style='background:linear-gradient(135deg,#6c5ce7,#a29bfe); padding:30px; text-align:center;'>
      <div style='font-size:3rem;'>💸</div>
      <h1 style='color:#fff; margin:10px 0 0; font-size:1.6rem;'>SplitWise Pro</h1>
    </div>
    <div style='padding:40px 36px;'>
      <h2 style='color:#a29bfe; margin:0 0 12px;'>Password Reset Request</h2>
      <p style='color:#d0d0d0; line-height:1.6;'>Hi {toName},</p>
      <p style='color:#d0d0d0; line-height:1.6;'>We received a request to reset your password. Use the OTP below:</p>
      <div style='background:#0f0e17; border:2px dashed #6c5ce7; border-radius:16px; padding:28px; text-align:center; margin:24px 0;'>
        <div style='font-size:3rem; font-weight:900; letter-spacing:0.4em; color:#a29bfe; font-family:monospace;'>{otp}</div>
        <p style='color:#888; font-size:0.85rem; margin:12px 0 0;'>⏱️ Valid for <strong style='color:#a29bfe;'>10 minutes</strong> only</p>
      </div>
      <p style='color:#d0d0d0; line-height:1.6;'>If you didn't request this, you can safely ignore this email. Your password will not change.</p>
      <p style='color:#d0d0d0; margin-top:32px;'>— The SplitWise Pro Team 💜</p>
    </div>
    <div style='padding:20px; text-align:center; background:#0f0e17; border-top:1px solid rgba(108,92,231,0.2);'>
      <p style='color:#555; font-size:0.8rem; margin:0;'>This is an automated email. Do not reply.</p>
    </div>
  </div>
</body>
</html>";

        await SendAsync(toEmail, subject, body, ct);
    }

    public async Task SendPasswordChangedEmailAsync(string toEmail, string toName, CancellationToken ct = default)
    {
        var subject = "✅ Your SplitWise Pro Password Was Changed";
        var body = $@"
<!DOCTYPE html>
<html>
<body style='font-family:Arial,sans-serif;'>
  <div style='max-width:480px;margin:40px auto;background:#1a1a2e;border-radius:20px;padding:36px;color:#fffffe;border:1px solid rgba(108,92,231,0.3);'>
    <h2 style='color:#00b894;'>✅ Password Changed Successfully</h2>
    <p style='color:#d0d0d0;'>Hi {toName}, your SplitWise Pro account password was just changed.</p>
    <p style='color:#d0d0d0;'>If you did not make this change, please contact support immediately and reset your password again.</p>
    <p style='color:#888;margin-top:32px;font-size:0.85rem;'>— SplitWise Pro Team</p>
  </div>
</body>
</html>";

        await SendAsync(toEmail, subject, body, ct);
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        // Default to Gmail if not specified
        var host     = _config["Email:SmtpHost"]     ?? "smtp.gmail.com";
        var portStr  = _config["Email:SmtpPort"]     ?? "587";
        var from     = _config["Email:From"]         ?? _config["Email:Username"];
        var fromName = _config["Email:FromName"]     ?? "Samatva App";
        var username = _config["Email:Username"]     ?? "";
        var password = _config["Email:Password"]     ?? "";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("Email credentials not configured. Skipping email to {Email}. OTP logged to debug.", toEmail);
            _logger.LogDebug("📧 [DEV] Email to {Email} — Subject: {Subject}\nBody: {Body}", toEmail, subject, htmlBody);
            return;
        }

        try
        {
            using var client = new SmtpClient(host, int.Parse(portStr))
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(username, password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };

            using var message = new MailMessage
            {
                From = new MailAddress(from ?? username, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(toEmail));

            await client.SendMailAsync(message, ct);
            _logger.LogInformation("✉️ Email sent successfully to {Email} via {Host}", toEmail, host);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
        }
    }
}
