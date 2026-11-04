namespace ShotsByMarques.Web.Services;

/// <summary>
/// Sends notification emails to the site owner (Smtp:ToEmail).
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an HTML email to the configured owner address.
    /// </summary>
    /// <param name="subject">Subject line of the email.</param>
    /// <param name="htmlBody">HTML body of the email.</param>
    /// <param name="replyTo">Optional address that replies should go to (the client's email).</param>
    Task SendAsync(string subject, string htmlBody, string? replyTo = null);
}
