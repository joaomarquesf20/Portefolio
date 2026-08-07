namespace ShotsByMarques.Web.Models;

public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    Cancelled = 2,
    Completed = 3
}

public enum OrderStatus
{
    PendingPayment = 0,
    Paid = 1,
    Cancelled = 2
}
