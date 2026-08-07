namespace ShotsByMarques.Web.Models;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int PhotoId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string FilePath { get; set; } = string.Empty;

    public Order? Order { get; set; }
}
