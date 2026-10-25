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

    [Required(ErrorMessage = "Escreve a zona ou localidade onde queres a sessão.")]
    [Display(Name = "Zona / localidade")]
    [StringLength(200)]
    public string Location { get; set; } = string.Empty;

    // ---- Computed by the server (travel + totals) ----
    public double? DistanceKm { get; set; }
    public decimal TravelFee { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public bool LocationRecognized { get; set; } = true;

    // ---- Context for the view ----
    public int Year { get; set; }
    public int Month { get; set; }
    public List<CalendarDay> Days { get; set; } = new();
    public List<CityOption> Cities { get; set; } = new();

    /// <summary>Distinct regions (e.g. "Porto", "Lisboa") for the first dropdown.</summary>
    public List<string> Regions { get; set; } = new();

    /// <summary>Region selected by the user (restored after validation errors).</summary>
    public string? SelectedRegion { get; set; }

    public TravelSettings Travel { get; set; } = new();

    /// <summary>Price of the mini-session type, used when an event day is selected
    /// (the client then books a mini session at the event). 0 when there is none.</summary>
    public decimal MiniPrice { get; set; }
}

public class CityOption
{
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public double Lat { get; set; }
    public double Lon { get; set; }
}

/// <summary>A time slot of the booking grid, with a flag for hours already taken
/// (overlapping bookings) or already past — shown disabled so the client sees
/// the full daily grid instead of wondering why some hours are missing.</summary>
public class SlotOption
{
    public string Time { get; set; } = string.Empty;
    public bool Taken { get; set; }
}

public class BookingMineViewModel
{
    public List<Booking> Bookings { get; set; } = new();

    /// <summary>Private galleries linked to the client's bookings (their session photos).</summary>
    public List<Gallery> Galleries { get; set; } = new();
}

public class BookingsIndexViewModel
{
    public List<SessionType> SessionTypes { get; set; } = new();
    public List<MiniSessionDayViewModel> MiniDays { get; set; } = new();
}

/// <summary>An event day offering mini sessions (from the admin calendar), with remaining spots.</summary>
public class MiniSessionDayViewModel
{
    public DateTime Date { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>Zone where the event takes place (e.g. "Porto").</summary>
    public string? Zone { get; set; }

    /// <summary>Event locality (e.g. "Parque da Cidade").</summary>
    public string? Location { get; set; }

    public int MaxMiniSessions { get; set; }
    public int Booked { get; set; }
    public decimal Price { get; set; }
    public int Remaining => Math.Max(0, MaxMiniSessions - Booked);
}

public class MiniSessionBookingViewModel
{
    public DateTime Date { get; set; }
    public string EventTitle { get; set; } = string.Empty;

    /// <summary>Zone where the event takes place (e.g. "Porto").</summary>
    public string? Zone { get; set; }

    /// <summary>Event locality (e.g. "Parque da Cidade").</summary>
    public string? Location { get; set; }

    public int MaxMiniSessions { get; set; }
    public int Booked { get; set; }
    public decimal Price { get; set; }
    public int Remaining => Math.Max(0, MaxMiniSessions - Booked);

    /// <summary>Whether the admin already scheduled the event's mini-session hours.</summary>
    public bool HasSchedule { get; set; }

    /// <summary>Available mini-session slots for the event day ("HH:mm").</summary>
    public List<string> Slots { get; set; } = new();

    /// <summary>Full mini-session slot grid with taken flags (for rendering).</summary>
    public List<SlotOption> SlotOptions { get; set; } = new();

    [Required(ErrorMessage = "Escolhe um horário.")]
    [Display(Name = "Horário")]
    public string TimeSlot { get; set; } = string.Empty;

    /// <summary>Half of the price, charged at booking — mini sessions follow the same rule as normal sessions.</summary>
    public decimal DepositAmount { get; set; }

    /// <summary>The other half, charged when the photos are delivered.</summary>
    public decimal BalanceAmount { get; set; }
}

/// <summary>
/// Confirmation shown after a Stripe deposit/balance payment. Carries the
/// booking reference and details so the page can render a real receipt and
/// offer the "Adicionar ao Calendário" (.ics / Google Calendar) actions —
/// the calendar endpoints stay owner-gated, exactly as before.
/// </summary>
public class PaymentResultViewModel
{
    /// <summary>"paid" | "processing" | "unpaid" — mirrors the controller's payment states.</summary>
    public string State { get; set; } = "unpaid";

    /// <summary>True when the flow was the balance payment (false = deposit).</summary>
    public bool IsBalance { get; set; }

    /// <summary>Auto-refresh seconds while Stripe runs an async action (MB Way), empty otherwise.</summary>
    public string MetaRefresh { get; set; } = string.Empty;

    /// <summary>Booking shown on the receipt; null when the Stripe session didn't match.</summary>
    public int? BookingId { get; set; }

    public string SessionName { get; set; } = string.Empty;

    /// <summary>Session start in Portugal's clock (already converted by the controller).</summary>
    public DateTime? Start { get; set; }

    public string Location { get; set; } = string.Empty;
}
