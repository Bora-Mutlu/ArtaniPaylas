using System.Security.Claims;
using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ArtaniPaylas.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IWebHostEnvironment _environment;

    public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, IWebHostEnvironment environment)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _environment = environment;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl ?? Url.Content("~/");
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return RedirectToAction("Index", "Admin");
            }
            return RedirectToAction("Index", "UserDashboard");
        }

        ModelState.AddModelError(string.Empty, "Geçersiz giriş denemesi. Bilgilerinizi kontrol edip tekrar deneyin.");
        return View(model);
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl ?? Url.Content("~/");

        if (!ModelState.IsValid) return View(model);

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing != null)
        {
            ModelState.AddModelError(string.Empty, "Bu e-posta adresi sistemde zaten kayıtlı.");
            return View(model);
        }

        string? finalImagePath = null;
        if (model.ProfileImageFile is not null)
        {
            var (path, err) = await SaveProfileImageAsync(model.ProfileImageFile);
            if (!string.IsNullOrWhiteSpace(err))
            {
                ModelState.AddModelError(string.Empty, err);
                return View(model);
            }
            finalImagePath = path;
        }

        var user = new ApplicationUser
        {
            UserName = model.Email, Email = model.Email, FullName = model.FullName,
            PhoneNumber = model.PhoneNumber, Age = model.Age, Location = model.Location,
            ProfileImageUrl = finalImagePath, EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(returnUrl ?? Url.Content("~/"));
        }

        if (finalImagePath != null && !finalImagePath.StartsWith("http"))
        {
            var p = Path.Combine(_environment.WebRootPath, finalImagePath.TrimStart('/'));
            if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, MapIdentityError(error));
        }
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(string? returnUrl = null)
    {
        await _signInManager.SignOutAsync();
        if (returnUrl != null) return LocalRedirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    private async Task<(string? PhotoPath, string? Error)> SaveProfileImageAsync(IFormFile file)
    {
        if (file.Length <= 0) return (null, "Dosya boş olamaz.");
        if (file.Length > 5 * 1024 * 1024) return (null, "Profil fotoğrafı boyutu 5 MB'ı aşamaz.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp")
            return (null, "Sadece JPG, PNG veya WEBP yükleyebilirsiniz.");

        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
        Directory.CreateDirectory(uploadsFolder);
        var fn = $"{Guid.NewGuid()}{ext}";
        var p = Path.Combine(uploadsFolder, fn);
        using var stream = new FileStream(p, FileMode.Create);
        await file.CopyToAsync(stream);
        return ($"/uploads/profiles/{fn}", null);
    }

    private static string MapIdentityError(IdentityError error)
    {
        return error.Code switch
        {
            "DuplicateEmail" => "E-posta adresi zaten kullanımda.",
            "DuplicateUserName" => "Kullanıcı adı / E-posta zaten kullanımda.",
            "PasswordTooShort" => "Şifreniz çok kısa.",
            "PasswordRequiresNonAlphanumeric" => "Şifreniz en az bir sembol içermelidir (örn. !,?,*).",
            "PasswordRequiresUpper" => "Şifreniz en az bir büyük harf içermelidir.",
            "PasswordRequiresDigit" => "Şifreniz en az bir rakam içermelidir.",
            "PasswordMismatch" => "Girdiğiniz şifreler eşleşmiyor.",
            _ => error.Description
        };
    }
}
