using System.Net;
using System.Net.Mail;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using SrSimplifyRoutine.Web.Data;

namespace SrSimplifyRoutine.Web.Services;

public sealed class RoutineEmailSender(IConfiguration config, IWebHostEnvironment environment, ILogger<RoutineEmailSender> logger) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your SR Simplify Routine account", $"Confirm your email to start planning: <a href=\"{TrustedLink(confirmationLink)}\">Confirm email</a>");
    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your SR Simplify Routine password", $"<a href=\"{TrustedLink(resetLink)}\">Reset password</a>. Ignore this email if you did not request it.");
    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "SR Simplify Routine reset code", HtmlEncoder.Default.Encode(resetCode));

    private string TrustedLink(string encoded)
    {
        var source = new Uri(WebUtility.HtmlDecode(encoded));
        var origin = config["Application:PublicOrigin"];
        var target = string.IsNullOrWhiteSpace(origin) && environment.IsDevelopment()
            ? source : new Uri(new Uri(origin!), source.PathAndQuery);
        return HtmlEncoder.Default.Encode(target.AbsoluteUri);
    }

    private async Task SendAsync(string email, string subject, string html)
    {
        if (environment.IsDevelopment() && string.IsNullOrWhiteSpace(config["Smtp:Host"]))
        {
            // Deliberately outside wwwroot. The website never serves confirmation/reset tokens.
            var directory = Path.Combine(environment.ContentRootPath, ".dev-mail");
            Directory.CreateDirectory(directory);
            var body = $"<!doctype html><meta charset=\"utf-8\"><h1>{HtmlEncoder.Default.Encode(subject)}</h1><p>To: {HtmlEncoder.Default.Encode(email)}</p><p>{html}</p>";
            await File.WriteAllTextAsync(Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.html"), body);
            logger.LogInformation("Development email written to the private .dev-mail directory.");
            return;
        }
        using var message = new MailMessage(config["Smtp:From"]!, email, subject, html) { IsBodyHtml = true };
        using var smtp = new SmtpClient(config["Smtp:Host"], config.GetValue("Smtp:Port", 587))
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(config["Smtp:Username"], config["Smtp:Password"]),
            Timeout = 15000
        };
        try { await smtp.SendMailAsync(message); }
        catch (SmtpException)
        {
            // Do not disclose delivery state or recipients through anonymous authentication responses.
            logger.LogError("Account email delivery failed. Check SMTP configuration and use resend after recovery.");
        }
    }
}
