using System.Net;
using System.Net.Mail;
using CarShowJudging.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarShowJudging.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var host = _config["Smtp:Host"];
        if (string.IsNullOrEmpty(host))
        {
            // Dev fallback — log the email so password reset links are visible without SMTP
            _logger.LogInformation(
                "SMTP not configured. Would have sent email to {To}.\nSubject: {Subject}\n{Body}",
                toEmail, subject, htmlBody);
            return;
        }

        var port = int.Parse(_config["Smtp:Port"] ?? "587");
        var from = _config["Smtp:From"] ?? "noreply@carshow.local";
        var user = _config["Smtp:User"];
        var pass = _config["Smtp:Password"];

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = !string.IsNullOrEmpty(user)
                ? new NetworkCredential(user, pass)
                : null
        };

        using var message = new MailMessage(from, toEmail, subject, htmlBody) { IsBodyHtml = true };
        await client.SendMailAsync(message);
    }
}
