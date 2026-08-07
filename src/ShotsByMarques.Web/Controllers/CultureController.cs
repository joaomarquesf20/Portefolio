using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ShotsByMarques.Web.Controllers;

public class CultureController : Controller
{
    /// <summary>Sets the culture cookie and returns to the previous page.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Set(string culture, string? returnUrl)
    {
        var cookie = CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));
        Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, cookie,
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }
}
