using System.ComponentModel.DataAnnotations;
using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.ViewModels;

public class BookingCreateViewModel
{
    public int SessionTypeId { get; set; }
    public SessionType? SessionType { get; set; }

    [Required(ErrorMessage = "Escolhe uma data.")]
    [Display(Name = "Data")]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Escolhe um horário.")]
    [Display(Name = "Horário")]
    public string TimeSlot { get; set; } = string.Empty;

    [Display(Name = "Notas (opcional)")]
    [StringLength(500)]
    public string? Notes { get; set; }
}

public class BookingMineViewModel
{
    public List<Booking> Bookings { get; set; } = new();

    /// <summary>Private galleries linked to the client's bookings (their session photos).</summary>
    public List<Gallery> Galleries { get; set; } = new();
}
