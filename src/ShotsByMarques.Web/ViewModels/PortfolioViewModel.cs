using ShotsByMarques.Web.Models;

namespace ShotsByMarques.Web.ViewModels;

public class PortfolioViewModel
{
    public List<Gallery> Sessions { get; set; } = new();
    public List<Gallery> Events { get; set; } = new();
}
