using System.ComponentModel.DataAnnotations;

namespace ShotsByMarques.Web.Models;

public class ContactMessage
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;
}
