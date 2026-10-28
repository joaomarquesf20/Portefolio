using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.ViewModels;
using System.Security.Claims;

namespace ShotsByMarques.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider _schemeProvider;

    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider schemeProvider)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _schemeProvider = schemeProvider;
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName
            };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
        }
        return View(model);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl ?? Url.Content("~/");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewData["ReturnUrl"] = model.ReturnUrl ?? Url.Content("~/");
        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, "Email ou palavra-passe inválidos.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // ---- External (Google) login ----
    // The Google scheme is only registered when its credentials exist in
    // configuration (see Program.cs), so the challenge is guarded — an
    // unconfigured server shows a friendly message instead of throwing.

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalLogin(string provider, string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        var scheme = await _schemeProvider.GetSchemeAsync(provider);
        if (scheme is null)
        {
            TempData["ExternalLoginError"] = "O login com Google não está configurado neste servidor.";
            return RedirectToAction(nameof(Login));
        }

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        returnUrl ??= Url.Content("~/");

        IActionResult Fail(string message)
        {
            TempData["ExternalLoginError"] = message;
            return RedirectToAction(nameof(Login));
        }

        if (!string.IsNullOrEmpty(remoteError))
            return Fail($"O Google devolveu um erro: {remoteError}");

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info is null)
            return Fail("Não foi possível ler a autenticação Google. Tenta novamente.");

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
            return Fail("O Google não devolveu um email utilizável para esta conta.");

        // Google marks the account's primary address as verified; never match
        // on an unverified address when the claim is present.
        var verified = info.Principal.FindFirstValue("urn:google:email_verified");
        if (verified is not null && !string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase))
            return Fail("O email desta conta Google ainda não está verificado.");

        // Email-matching sign-in: the verified Google address must belong to an
        // account that already exists — the seeded admin account
        // (DbSeeder.AdminEmail, marquesjoao2006@gmail.com) or any registered
        // client. Unknown addresses are denied; no account is auto-created.
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return Fail("Esta conta Google não corresponde a nenhum utilizador registado.");

        await _signInManager.SignInAsync(user, isPersistent: false);

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Home");
    }
}
