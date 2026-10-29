using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using ShotsByMarques.Web.Models;
using ShotsByMarques.Web.ViewModels;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace ShotsByMarques.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider _schemeProvider;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AccountController> _logger;

    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
        Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider schemeProvider,
        IEmailSender emailSender, ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _schemeProvider = schemeProvider;
        _emailSender = emailSender;
        _logger = logger;
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
                // Do not sign the user in yet: the account is only usable after
                // the confirmation link sent to the registered address is opened.
                try
                {
                    await SendConfirmationEmailAsync(user);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send the account confirmation email for user {UserId}.", user.Id);
                    TempData["ConfirmationEmailError"] =
                        "A conta foi criada, mas não foi possível enviar o email de confirmação agora. Usa o botão abaixo para tentar novamente.";
                }

                return RedirectToAction(nameof(RegisterConfirmation), new { email = model.Email });
            }
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
        }
        return View(model);
    }

    [HttpGet]
    public IActionResult RegisterConfirmation(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToAction(nameof(Login));

        ViewData["Email"] = email;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? code)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
            return ConfirmationResult(false, "O link de confirmação é inválido.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return ConfirmationResult(false, "A conta associada a este link já não existe.");

        if (await _userManager.IsEmailConfirmedAsync(user))
            return ConfirmationResult(true, "O teu email já estava confirmado. Já podes iniciar sessão.");

        var result = await _userManager.ConfirmEmailAsync(user, code);
        if (!result.Succeeded)
            return ConfirmationResult(false, "O link de confirmação é inválido ou expirou. Pede um novo email.");

        return ConfirmationResult(true, "Email confirmado com sucesso. Já podes iniciar sessão.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmationEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["ConfirmationEmailError"] = "Indica o email da conta.";
            return RedirectToAction(nameof(Login));
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is not null && !await _userManager.IsEmailConfirmedAsync(user))
        {
            try
            {
                await SendConfirmationEmailAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resend the account confirmation email for user {UserId}.", user.Id);
                TempData["ConfirmationEmailError"] =
                    "Não foi possível enviar o email de confirmação agora. Tenta novamente mais tarde.";
                return RedirectToAction(nameof(RegisterConfirmation), new { email });
            }
        }

        // Same response for unknown or already-confirmed addresses, so the
        // endpoint cannot be used to discover registered accounts.
        TempData["ConfirmationEmailSent"] =
            "Se existir uma conta por confirmar com este email, enviámos um novo link.";
        return RedirectToAction(nameof(RegisterConfirmation), new { email });
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

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty,
                "A conta ainda não está confirmada. Verifica o teu email e abre o link de confirmação.");
            ViewData["ShowResendConfirmation"] = true;
            return View(model);
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

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        var email = user.Email;
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("The user does not have an email address.");

        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var callbackUrl = Url.Action(
            nameof(ConfirmEmail),
            "Account",
            new { userId = user.Id, code },
            Request.Scheme)
            ?? throw new InvalidOperationException("Could not build the email confirmation link.");

        var safeUrl = HtmlEncoder.Default.Encode(callbackUrl);
        var html = $"""
            <p>Olá,</p>
            <p>Obrigado por criares uma conta no <strong>Shots By Marques</strong>.</p>
            <p>Para ativares a tua conta, confirma o teu email através do link abaixo:</p>
            <p><a href="{safeUrl}">Confirmar email</a></p>
            <p>Se não criaste esta conta, podes ignorar este email.</p>
            <p>— Shots By Marques</p>
            """;

        await _emailSender.SendEmailAsync(email, "Confirma o teu email — Shots By Marques", html);
    }

    private IActionResult ConfirmationResult(bool succeeded, string message)
    {
        ViewData["Confirmed"] = succeeded;
        ViewData["ConfirmationMessage"] = message;
        return View("ConfirmEmail");
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
