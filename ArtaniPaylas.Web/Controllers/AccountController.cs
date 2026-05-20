using System.Security.Claims;
using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.Helpers;
using ArtaniPaylas.Core.Interfaces;
using ArtaniPaylas.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ArtaniPaylas.Web.Services;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace ArtaniPaylas.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IWebHostEnvironment _environment;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IWebHostEnvironment environment,
        IEmailService emailService,
        INotificationService notificationService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _environment = environment;
        _emailService = emailService;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null, string? panel = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var isAdmin = currentUser != null && await _userManager.IsInRoleAsync(currentUser, "Admin");
            if (isAdmin)
            {
                return RedirectToAction("Index", "Admin");
            }

            if (currentUser != null && !currentUser.EmailConfirmedAt.HasValue && !string.IsNullOrWhiteSpace(currentUser.ConfirmationToken))
            {
                TempData["ErrorMessage"] = "E-posta adresiniz henüz doğrulanmadı. Lütfen paneldeki doğrulama bildirimini kullanın.";
                return RedirectToAction("Index", "UserDashboard");
            }

            return RedirectToAction("Index", "UserDashboard");
        }

        return RenderAuthPageAsync(
            activePanel: string.Equals(panel, "register", StringComparison.OrdinalIgnoreCase) ? "register" : "login",
            returnUrl: returnUrl,
            loginModel: new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([Bind(Prefix = "Login")] LoginViewModel model, string? returnUrl = null)
    {
        returnUrl ??= model.ReturnUrl;
        if (!ModelState.IsValid)
        {
            return RenderAuthPageAsync("login", returnUrl, loginModel: model);
        }

        var loginUser = await _userManager.FindByEmailAsync(model.Email);
        if (loginUser != null && !loginUser.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Hesabınız pasif durumdadır. Lütfen yönetici ile iletişime geçin.");
            return RenderAuthPageAsync("login", returnUrl, loginModel: model);
        }

        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Admin");

            if (user != null && !user.IsActive)
            {
                await _signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty, "Hesabınız pasif durumdadır. Lütfen yönetici ile iletişime geçin.");
                return RenderAuthPageAsync("login", returnUrl, loginModel: model);
            }

            if (user != null && !isAdmin && !user.EmailConfirmedAt.HasValue)
            {
                await SendConfirmationEmailAsync(user, isResend: true);
                TempData["ErrorMessage"] = "Hesaba giriş için önce e-posta doğrulaması gereklidir. Yeni doğrulama e-postası gönderildi.";
                return RedirectToAction("ConfirmationPending", new { email = model.Email });
            }

            if (isAdmin)
            {
                return RedirectToAction("Index", "Admin");
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "UserDashboard");
        }

        ModelState.AddModelError(string.Empty, "Geçersiz giriş denemesi. Bilgilerinizi kontrol edip tekrar deneyin.");
        return RenderAuthPageAsync("login", returnUrl, loginModel: model);
    }

    [HttpGet]
    public IActionResult Register(string? returnUrl = null)
    {
        return RedirectToAction(nameof(Login), new { returnUrl, panel = "register" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register([Bind(Prefix = "Register")] RegisterViewModel model, string? returnUrl = null)
    {
        returnUrl ??= model.ReturnUrl;

        if (!ModelState.IsValid)
        {
            return RenderAuthPageAsync("register", returnUrl, registerModel: model);
        }

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing != null)
        {
            ModelState.AddModelError(string.Empty, "Bu e-posta adresi sistemde zaten kayıtlı.");
            return RenderAuthPageAsync("register", returnUrl, registerModel: model);
        }

        string? finalImagePath = null;
        if (model.ProfileImageFile is not null)
        {
            var (path, err) = await SaveProfileImageAsync(model.ProfileImageFile);
            if (!string.IsNullOrWhiteSpace(err))
            {
                ModelState.AddModelError(string.Empty, err);
                return RenderAuthPageAsync("register", returnUrl, registerModel: model);
            }
            finalImagePath = path;
        }

        // Email verification token oluştur
        var confirmationToken = TokenHelper.GenerateRandomToken(32);
        var hashedToken = TokenHelper.HashToken(confirmationToken);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            Age = model.Age,
            Location = model.Location,
            ProfileImageUrl = finalImagePath,
            EmailConfirmed = false, // Doğrulanmamış
            EmailConfirmedAt = null, // Henüz doğrulanmamış
            ConfirmationToken = hashedToken,
            ConfirmationTokenExpiresAt = DateTime.UtcNow.AddHours(24) // 24 saat geçerli
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            var emailSent = true;

            // Bildirim oluştur
            await _notificationService.CreateNotificationAsync(
                user.Id,
                "E-posta Doğrulama Gerekli",
                "Hesabınızı kullanmak için lütfen e-posta adresinizi doğrulayın.",
                Core.Enums.NotificationType.EmailVerificationRequired);

            // Confirmation e-postası gönder
            var confirmationLink = Url.Action("ConfirmEmail", "Account",
                new { token = confirmationToken, email = model.Email },
                protocol: Request.Scheme);

            var emailBody = EmailTemplateBuilder.BuildEmailConfirmationTemplate(model.FullName, confirmationLink ?? string.Empty, isResend: false);

            try
            {
                await _emailService.SendEmailAsync(model.Email, "ArtaniPaylas - E-posta Doğrulama", emailBody);
            }
            catch (Exception ex)
            {
                emailSent = false;
                TempData["ErrorMessage"] = $"Hesap oluşturuldu ancak doğrulama e-postası gönderilemedi: {ex.Message}. SMTP ayarlarını kontrol edip tekrar gönderim yapabilirsiniz.";
            }

            if (emailSent)
            {
                TempData["SuccessMessage"] = "Hesabınız oluşturuldu. Doğrulama e-postası gönderildi. Lütfen gelen kutunuzu kontrol edin.";
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("ConfirmationPending", new { email = user.Email });
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
        return RenderAuthPageAsync("register", returnUrl, registerModel: model);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(string? returnUrl = null)
    {
        await _signInManager.SignOutAsync();
        if (returnUrl != null) return LocalRedirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// E-posta doğrulama bekleniyor sayfası
    /// </summary>
    [HttpGet]
    public IActionResult ConfirmationPending(string email)
    {
        return View(new { Email = email });
    }

    /// <summary>
    /// E-posta doğrulama bağlantısı tıklandığında
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string token, string email)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(email))
        {
            return View("EmailConfirmationError", "Geçersiz doğrulama bağlantısı.");
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return View("EmailConfirmationError", "Kullanıcı bulunamadı.");
        }

        // Token hash'le ve kontrol et
        var hashedToken = TokenHelper.HashToken(token);
        if (user.ConfirmationToken != hashedToken)
        {
            return View("EmailConfirmationError", "Doğrulama bağlantısı geçersiz.");
        }

        // Token expire check
        if (user.ConfirmationTokenExpiresAt < DateTime.UtcNow)
        {
            return View("EmailConfirmationError", "Doğrulama bağlantısının süresi dolmuş. Lütfen yeni bir link talep edin.");
        }

        // Doğrulama başarılı
        user.EmailConfirmed = true;
        user.EmailConfirmedAt = DateTime.UtcNow;
        user.ConfirmationToken = null;
        user.ConfirmationTokenExpiresAt = null;

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            TempData["SuccessMessage"] = "E-posta doğrulaması tamamlandı. Hoş geldiniz.";

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return RedirectToAction("Index", "Admin");
            }

            return RedirectToAction("Index", "UserDashboard");
        }

        return View("EmailConfirmationError", "Doğrulama sırasında bir hata oluştu.");
    }

    /// <summary>
    /// Confirmation email'i tekrar gönder
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmationEmail(string email)
    {
        var isAuthenticated = User.Identity?.IsAuthenticated == true;

        if (string.IsNullOrWhiteSpace(email))
        {
            if (isAuthenticated)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                email = currentUser?.Email ?? string.Empty;
            }
            else
            {
                return RedirectToAction("ConfirmationPending");
            }
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Doğrulama e-postası gönderilemedi. Kullanıcı bulunamadı.";
            return isAuthenticated
                ? RedirectToAction("Index", "UserDashboard")
                : RedirectToAction("ConfirmationPending", new { email });
        }

        // Zaten doğrulanmış mı?
        if (user.EmailConfirmedAt.HasValue)
        {
            TempData["SuccessMessage"] = "E-posta adresiniz zaten doğrulanmış durumda.";
            return isAuthenticated
                ? RedirectToAction("Index", "UserDashboard")
                : RedirectToAction("Login");
        }

        try
        {
            await SendConfirmationEmailAsync(user, isResend: true);
            TempData["SuccessMessage"] = "Doğrulama e-postası yeniden gönderildi. Spam/Junk klasörünü de kontrol edin.";
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"E-posta gönderilemedi: {ex.Message}";
        }

        return isAuthenticated
            ? RedirectToAction("Index", "UserDashboard")
            : RedirectToAction("ConfirmationPending", new { email });
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user != null && user.IsActive)
        {
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetToken));
            var resetLink = Url.Action(
                "ResetPassword",
                "Account",
                new { email = user.Email, token = encodedToken },
                protocol: Request.Scheme);

            var emailBody = EmailTemplateBuilder.BuildActionEmailTemplate(
                "Sifre Sifirlama Talebi",
                "Hesap Güvenliği",
                user.FullName ?? user.UserName ?? "Kullanıcı",
                "Şifrenizi sıfırlamak için aşağıdaki butona tıklayın. Bu talebi siz oluşturmadıysanız e-postayı yok sayabilirsiniz.",
                resetLink,
                "Şifreyi Sıfırla");

            await _emailService.SendEmailAsync(user.Email!, "ArtaniPaylas - Şifre Sıfırlama", emailBody);
        }

        TempData["SuccessMessage"] = "Eğer e-posta adresi sistemde kayıtlıysa şifre sıfırlama bağlantısı gönderildi.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ResetPassword(string email, string token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            TempData["ErrorMessage"] = "Şifre sıfırlama bağlantısı geçersiz.";
            return RedirectToAction(nameof(Login));
        }

        return View(new ResetPasswordViewModel
        {
            Email = email,
            Token = token
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            TempData["SuccessMessage"] = "Şifreniz güncellendi. Yeni şifreniz ile giriş yapabilirsiniz.";
            return RedirectToAction(nameof(Login));
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
        }
        catch
        {
            ModelState.AddModelError(string.Empty, "Şifre sıfırlama bağlantısı geçersiz veya bozuk.");
            return View(model);
        }

        var resetResult = await _userManager.ResetPasswordAsync(user, decodedToken, model.Password);
        if (!resetResult.Succeeded)
        {
            foreach (var error in resetResult.Errors)
            {
                ModelState.AddModelError(string.Empty, MapIdentityError(error));
            }

            return View(model);
        }

        TempData["SuccessMessage"] = "Şifreniz başarıyla güncellendi. Giriş yapabilirsiniz.";
        return RedirectToAction(nameof(Login));
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

    private async Task SendConfirmationEmailAsync(ApplicationUser user, bool isResend)
    {
        var confirmationToken = TokenHelper.GenerateRandomToken(32);
        var hashedToken = TokenHelper.HashToken(confirmationToken);

        user.ConfirmationToken = hashedToken;
        user.ConfirmationTokenExpiresAt = DateTime.UtcNow.AddHours(24);

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            var firstError = updateResult.Errors.FirstOrDefault()?.Description ?? "Kullanıcı güncellenemedi.";
            throw new InvalidOperationException(firstError);
        }

        var confirmationLink = Url.Action("ConfirmEmail", "Account",
            new { token = confirmationToken, email = user.Email },
            protocol: Request.Scheme);

        var emailBody = EmailTemplateBuilder.BuildEmailConfirmationTemplate(user.FullName ?? user.UserName ?? "Kullanıcı", confirmationLink ?? string.Empty, isResend);
        var subject = isResend
            ? "ArtaniPaylas - E-posta Doğrulama (Yeniden)"
            : "ArtaniPaylas - E-posta Doğrulama";

        await _emailService.SendEmailAsync(user.Email!, subject, emailBody);
    }

    private IActionResult RenderAuthPageAsync(
        string activePanel,
        string? returnUrl,
        LoginViewModel? loginModel = null,
        RegisterViewModel? registerModel = null)
    {
        var effectiveReturnUrl = returnUrl ?? Url.Content("~/");

        var viewModel = new AccountAuthViewModel
        {
            ActivePanel = activePanel,
            ReturnUrl = effectiveReturnUrl,
            Login = loginModel ?? new LoginViewModel(),
            Register = registerModel ?? new RegisterViewModel()
        };

        viewModel.Login.ReturnUrl = effectiveReturnUrl;
        viewModel.Register.ReturnUrl = effectiveReturnUrl;

        return View("Login", viewModel);
    }

    private async Task<IActionResult> RedirectAfterSuccessfulSignInAsync(ApplicationUser user, string? returnUrl)
    {
        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            return RedirectToAction("Index", "Admin");
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "UserDashboard");
    }
}
