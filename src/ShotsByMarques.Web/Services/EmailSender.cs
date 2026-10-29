using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace ShotsByMarques.Web.Services;

/// <summary>
/// Sends transactional email through SMTP. All connection settings come from
/// configuration, never from source code: Smtp:Host, Smtp:Port, Smtp:Username,
/// Smtp:Password and Smtp:SenderEmail. In development these are managed with
/// dotnet user-secrets; on Render they are supplied as environment variables
/// (for example Smtp__Host, Smtp__Port, ...).
/// </summary>
public sealed class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("O destinatário do email é obrigatório.", nameof(email));

        var host = Required("Smtp:Host");
        var senderEmail = Required("Smtp:SenderEmail");
        var portValue = Required("Smtp:Port");
        if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "A configuração 'Smtp:Port' deve ser um número entre 1 e 65535.");
        }

        // Username/password are read from configuration as well. They may be
        // empty only for a local, unauthenticated SMTP relay (e.g. MailHog).
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
            Body = htmlMessage,
            IsBodyHtml = true
        };
        message.To.Add(email);

        using var client = new SmtpClient(host, port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = enableSsl,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password);

        await client.SendMailAsync(message);
        _logger.LogInformation("Email sent through SMTP host {SmtpHost}.", host);
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
