using System.Security.Claims;
using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.Enums;
using ArtaniPaylas.Core.ViewModels;
using ArtaniPaylas.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtaniPaylas.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private static readonly string[] AllowedPhotoExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string[] AllowedPhotoContentTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxPhotoSizeBytes = 5 * 1024 * 1024;

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _environment;

    public ProfileController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment environment)
    {
        _context = context;
        _userManager = userManager;
        _environment = environment;
    }

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var myListings = await _context.Listings
            .Where(x => x.OwnerUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var incoming = await _context.Requests
            .Include(x => x.RequesterUser)
            .Include(x => x.Listing)
            .Where(x => x.Listing != null && x.Listing.OwnerUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var outgoing = await _context.Requests
            .Include(x => x.Listing)
            .ThenInclude(x => x!.OwnerUser)
            .Where(x => x.RequesterUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var model = new ProfileIndexViewModel
        {
            User = user,
            MyListings = myListings,
            IncomingRequests = incoming,
            OutgoingRequests = outgoing
        };

        return View(model);
    }

    public async Task<IActionResult> History()
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var deliveredIncoming = await _context.Requests
            .Include(x => x.RequesterUser)
            .Include(x => x.Listing)
            .Where(x => x.Status == RequestStatus.Delivered && x.Listing != null && x.Listing.OwnerUserId == userId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync();

        var deliveredOutgoing = await _context.Requests
            .Include(x => x.Listing)
            .ThenInclude(x => x!.OwnerUser)
            .Where(x => x.Status == RequestStatus.Delivered && x.RequesterUserId == userId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync();

        var receivedReviews = await _context.Reviews
            .Include(x => x.FromUser)
            .Where(x => x.ToUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var givenReviews = await _context.Reviews
            .Include(x => x.ToUser)
            .Where(x => x.FromUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var model = new ProfileHistoryViewModel
        {
            DeliveredIncomingRequests = deliveredIncoming,
            DeliveredOutgoingRequests = deliveredOutgoing,
            ReceivedReviews = receivedReviews,
            GivenReviews = givenReviews
        };

        return View(model);
    }

    public async Task<IActionResult> Edit()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var model = new ProfileEditViewModel
        {
            FullName = user.FullName ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            Age = user.Age ?? 18,
            Location = user.Location ?? string.Empty,
            ProfileImageUrl = user.ProfileImageUrl
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProfileEditViewModel model, IFormFile? profileImageFile)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var profileImageValue = model.ProfileImageUrl?.Trim();

        if (profileImageFile is not null && profileImageFile.Length > 0)
        {
            var (uploadedPath, uploadError) = await SaveProfileImageAsync(profileImageFile);
            if (!string.IsNullOrWhiteSpace(uploadError))
            {
                ModelState.AddModelError("profileImageFile", uploadError);
                return View(model);
            }

            profileImageValue = uploadedPath;
        }
        else if (!string.IsNullOrWhiteSpace(profileImageValue) && !IsAllowedProfileImageValue(profileImageValue))
        {
            ModelState.AddModelError(nameof(model.ProfileImageUrl), "Lütfen geçerli bir görsel linki gir (http/https). Cihazdan yükleme yaptıysan bu alanı boş bırakabilirsin.");
            return View(model);
        }

        user.FullName = model.FullName;
        user.PhoneNumber = model.PhoneNumber;
        user.Age = model.Age;
        user.Location = model.Location;
        user.ProfileImageUrl = string.IsNullOrWhiteSpace(profileImageValue) ? null : profileImageValue;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        TempData["SuccessMessage"] = "Profil bilgileri güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    private static bool IsAllowedProfileImageValue(string value)
    {
        if (value.StartsWith("/uploads/profiles/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private async Task<(string? PhotoPath, string? Error)> SaveProfileImageAsync(IFormFile file)
    {
        if (file.Length > MaxPhotoSizeBytes)
        {
            return (null, "Profil fotoğrafı en fazla 5 MB olabilir.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedPhotoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return (null, "Sadece JPG, PNG ve WEBP dosyaları yükleyebilirsin.");
        }

        if (string.IsNullOrWhiteSpace(file.ContentType) ||
            !AllowedPhotoContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return (null, "Geçersiz dosya tipi tespit edildi.");
        }

        var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
        Directory.CreateDirectory(uploadsPath);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(uploadsPath, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        return ($"/uploads/profiles/{fileName}", null);
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
