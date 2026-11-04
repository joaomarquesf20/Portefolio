using System.Net;
using System.Net.Mail;

namespace ShotsByMarques.Web.Services;

/// <summary>
/// Sends owner notification emails through SMTP. All settings come from
/// configuration (Smtp:Host, Port, EnableSsl, Username, Password,
/// SenderEmail, ToEmail), never from source code. In development these are
/// managed with dotnet user-secrets; in production they are environment
/// variables (for example Smtp__Password).
/// </summary>
public sealed class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string subject, string htmlBody, string? replyTo = null)
    {
        var host = Required("Smtp:Host");
        var senderEmail = Required("Smtp:SenderEmail");
        var toEmail = Required("Smtp:ToEmail");
        var portValue = Required("Smtp:Port");
        if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "A configuração 'Smtp:Port' deve ser um número entre 1 e 65535.");
        }

        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        if (string.IsNullOrWhiteSpace(username) != string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "As configurações 'Smtp:Username' e 'Smtp:Password' têm de ser fornecidas em conjunto.");
        }

        var enableSsl = !bool.TryParse(_configuration["Smtp:EnableSsl"], out var parsedSsl) || parsedSsl;

        using var message = new MailMessage
        {
            From = new MailAddress(senderEmail, "Shots By Marques"),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            message.ReplyToList.Add(new MailAddress(replyTo));
        }

        using var client = new SmtpClient(host, port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = enableSsl,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password);

        await client.SendMailAsync(message);
        _logger.LogInformation("Contact notification sent through SMTP host {SmtpHost}.", host);
    }

    private string Required(string key)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"A configuração SMTP '{key}' não está definida. Usa dotnet user-secrets em desenvolvimento ou uma variável de ambiente em produção.");
        }

        return value;
    }
}
