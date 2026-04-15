using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ArtaniPaylas.Web.Controllers;

[AllowAnonymous]
[Route("Admin")]
public class AdminAuthController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AdminAuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet("Login")]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View("~/Views/Admin/AdminLogin.cshtml", new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("Login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl ?? Url.Content("~/Admin");
        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/AdminLogin.cshtml", model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user != null)
        {
            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    return LocalRedirect(returnUrl ?? Url.Content("~/Admin"));
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Bu panele giriş yetkiniz bulunmamaktadır.");
                return View("~/Views/Admin/AdminLogin.cshtml", model);
            }
        }

        ModelState.AddModelError(string.Empty, "Geçersiz giriş denemesi. Bilgilerinizi kontrol edip tekrar deneyin.");
        return View("~/Views/Admin/AdminLogin.cshtml", model);
    }
}
