using System.Security.Claims;
using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.Enums;
using ArtaniPaylas.Core.Interfaces;
using ArtaniPaylas.Core.ViewModels;
using ArtaniPaylas.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtaniPaylas.Web.Controllers;

public class ListingsController : Controller
{
    private static readonly string[] AllowedPhotoExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string[] AllowedPhotoContentTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxPhotoSizeBytes = 5 * 1024 * 1024;
    private const int MaxPhotoCount = 6;

    private readonly ApplicationDbContext _context;
    private readonly IListingStatusService _listingStatusService;
    private readonly INotificationService _notificationService;
    private readonly IWebHostEnvironment _environment;

    public ListingsController(
        ApplicationDbContext context,
        IListingStatusService listingStatusService,
        INotificationService notificationService,
        IWebHostEnvironment environment)
    {
        _context = context;
        _listingStatusService = listingStatusService;
        _notificationService = notificationService;
        _environment = environment;
    }

    [AllowAnonymous]
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Home", null, "listings");
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        await _listingStatusService.UpdateExpiredListingsAsync();

        var listing = await _context.Listings
            .Include(x => x.OwnerUser)
            .Include(x => x.Photos.OrderBy(p => p.SortOrder))
            .Include(x => x.Requests.OrderByDescending(r => r.CreatedAt))
            .ThenInclude(x => x.RequesterUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (listing is null)
        {
            return NotFound();
        }

        return View(listing);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        return View(new ListingCreateEditViewModel());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ListingCreateEditViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || !user.EmailConfirmedAt.HasValue)
        {
            ModelState.AddModelError(string.Empty, "İlan oluşturmak için lütfen e-posta adresinizi doğrulayın.");
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var listing = new Listing
        {
            OwnerUserId = userId,
            Title = model.Title,
            Description = model.Description,
            Category = model.Category,
            Size = NormalizeText(model.Size),
            Gender = NormalizeText(model.Gender),
            AgeGroup = NormalizeText(model.AgeGroup),
            Condition = NormalizeText(model.Condition),
            ExpirationDate = NormalizeExpirationDateToUtc(model.ExpirationDate),
            Location = model.Location,
            District = NormalizeText(model.District),
            ContactNote = NormalizeText(model.ContactNote),
            Status = ListingStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var (savedPhotos, photoError) = await SavePhotosAsync(model.Photos);
        if (!string.IsNullOrWhiteSpace(photoError))
        {
            ModelState.AddModelError(nameof(model.Photos), photoError);
            return View(model);
        }

        listing.PhotoPath = savedPhotos.FirstOrDefault();
        listing.Photos = savedPhotos
            .Select((path, index) => new ListingPhoto
            {
                PhotoPath = path,
                SortOrder = index
            })
            .ToList();

        _context.Listings.Add(listing);
        await _context.SaveChangesAsync();
        await NotifySubscribersForNewListingAsync(listing, userId);

        TempData["SuccessMessage"] = "Kıyafet ilanı yayınlandı.";
        return RedirectToAction("Listings", "Admin");
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var listing = await _context.Listings
            .Include(x => x.Photos.OrderBy(p => p.SortOrder))
            .FirstOrDefaultAsync(x => x.Id == id);

        if (listing is null)
        {
            return NotFound();
        }

        if (!CanManageListing())
        {
            return Forbid();
        }

        if (listing.Status != ListingStatus.Active)
        {
            TempData["ErrorMessage"] = "Kapanan veya tamamlanan ilanlar düzenlenemez.";
            return RedirectToAction(nameof(MyListings));
        }

        var model = new ListingCreateEditViewModel
        {
            Id = listing.Id,
            Title = listing.Title,
            Description = listing.Description,
            Category = listing.Category,
            Size = listing.Size,
            Gender = listing.Gender,
            AgeGroup = listing.AgeGroup,
            Condition = listing.Condition,
            ExpirationDate = listing.ExpirationDate,
            Location = listing.Location,
            District = listing.District,
            ContactNote = listing.ContactNote,
            ExistingPhotoPaths = listing.Photos.Select(x => x.PhotoPath).ToList(),
            ExistingPhotos = listing.Photos
                .Select(x => new ExistingListingPhotoViewModel
                {
                    Id = x.Id,
                    PhotoPath = x.PhotoPath
                })
                .ToList()
        };

        if (model.ExistingPhotoPaths.Count == 0 && !string.IsNullOrWhiteSpace(listing.PhotoPath))
        {
            model.ExistingPhotoPaths.Add(listing.PhotoPath);
            model.ExistingPhotos.Add(new ExistingListingPhotoViewModel
            {
                Id = 0,
                PhotoPath = listing.PhotoPath
            });
        }

        return View(model);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ListingCreateEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var listing = await _context.Listings
            .Include(x => x.Photos.OrderBy(p => p.SortOrder))
            .FirstOrDefaultAsync(x => x.Id == id);

        if (listing is null)
        {
            return NotFound();
        }

        if (!CanManageListing())
        {
            return Forbid();
        }

        if (listing.Status != ListingStatus.Active)
        {
            TempData["ErrorMessage"] = "Kapanan veya tamamlanan ilanlar düzenlenemez.";
            return RedirectToAction(nameof(MyListings));
        }

        if (!ModelState.IsValid)
        {
            PopulateExistingPhotos(model, listing);
            return View(model);
        }

        listing.Title = model.Title;
        listing.Description = model.Description;
        listing.Category = model.Category;
        listing.Size = NormalizeText(model.Size);
        listing.Gender = NormalizeText(model.Gender);
        listing.AgeGroup = NormalizeText(model.AgeGroup);
        listing.Condition = NormalizeText(model.Condition);
        listing.ExpirationDate = NormalizeExpirationDateToUtc(model.ExpirationDate);
        listing.Location = model.Location;
        listing.District = NormalizeText(model.District);
        listing.ContactNote = NormalizeText(model.ContactNote);

        var deletedPhotoIds = model.DeletedPhotoIds.Distinct().ToHashSet();
        var photosToDelete = listing.Photos
            .Where(x => deletedPhotoIds.Contains(x.Id))
            .ToList();
        var clearLegacyPhotoPath = deletedPhotoIds.Contains(0) && listing.Photos.Count == 0 && !string.IsNullOrWhiteSpace(listing.PhotoPath);
        var remainingPhotoCount = listing.Photos.Count - photosToDelete.Count + model.Photos.Count(x => x.Length > 0);

        if (clearLegacyPhotoPath)
        {
            remainingPhotoCount--;
        }

        if (remainingPhotoCount > MaxPhotoCount)
        {
            ModelState.AddModelError(nameof(model.Photos), $"Bir ilanda en fazla {MaxPhotoCount} fotoğraf olabilir. Önce mevcut fotoğraflardan bazılarını kaldır.");
            PopulateExistingPhotos(model, listing);
            return View(model);
        }

        if (remainingPhotoCount <= 0)
        {
            ModelState.AddModelError(nameof(model.Photos), "İlanda en az bir fotoğraf kalmalı veya yeni fotoğraf yüklemelisin.");
            PopulateExistingPhotos(model, listing);
            return View(model);
        }

        var (newPhotoPaths, photoError) = await SavePhotosAsync(model.Photos);
        if (!string.IsNullOrWhiteSpace(photoError))
        {
            ModelState.AddModelError(nameof(model.Photos), photoError);
            PopulateExistingPhotos(model, listing);
            return View(model);
        }

        var pathsToDelete = photosToDelete.Select(x => x.PhotoPath).ToList();

        foreach (var photo in photosToDelete)
        {
            listing.Photos.Remove(photo);
            _context.ListingPhotos.Remove(photo);
        }

        if (clearLegacyPhotoPath)
        {
            pathsToDelete.Add(listing.PhotoPath!);
            listing.PhotoPath = null;
        }

        if (newPhotoPaths.Count > 0)
        {
            var startOrder = listing.Photos.Any() ? listing.Photos.Max(x => x.SortOrder) + 1 : 0;
            foreach (var item in newPhotoPaths.Select((path, index) => new ListingPhoto
                     {
                         ListingId = listing.Id,
                         PhotoPath = path,
                         SortOrder = startOrder + index
                     }))
            {
                listing.Photos.Add(item);
            }
        }

        listing.PhotoPath = listing.Photos
            .OrderBy(x => x.SortOrder)
            .Select(x => x.PhotoPath)
            .FirstOrDefault() ?? listing.PhotoPath;

        await _context.SaveChangesAsync();

        foreach (var path in pathsToDelete)
        {
            DeletePhotoFile(path);
        }

        TempData["SuccessMessage"] = "İlan güncellendi.";
        return RedirectToAction("Listings", "Admin");
    }

    private static void PopulateExistingPhotos(ListingCreateEditViewModel model, Listing listing)
    {
        model.ExistingPhotos = listing.Photos
            .OrderBy(x => x.SortOrder)
            .Select(x => new ExistingListingPhotoViewModel
            {
                Id = x.Id,
                PhotoPath = x.PhotoPath
            })
            .ToList();
        model.ExistingPhotoPaths = model.ExistingPhotos.Select(x => x.PhotoPath).ToList();

        if (model.ExistingPhotos.Count == 0 && !string.IsNullOrWhiteSpace(listing.PhotoPath))
        {
            model.ExistingPhotos.Add(new ExistingListingPhotoViewModel
            {
                Id = 0,
                PhotoPath = listing.PhotoPath
            });
            model.ExistingPhotoPaths.Add(listing.PhotoPath);
        }
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var listing = await _context.Listings.FirstOrDefaultAsync(x => x.Id == id);
        if (listing is null)
        {
            return NotFound();
        }

        if (!CanManageListing())
        {
            return Forbid();
        }

        return View(listing);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var listing = await _context.Listings.FirstOrDefaultAsync(x => x.Id == id);
        if (listing is null)
        {
            return NotFound();
        }

        if (!CanManageListing())
        {
            return Forbid();
        }

        _context.Listings.Remove(listing);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "İlan silindi.";
        return RedirectToAction("Listings", "Admin");
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MyListings(bool activeOnly = false)
    {
        await _listingStatusService.UpdateExpiredListingsAsync();

        var query = _context.Listings
            .Include(x => x.Photos.OrderBy(p => p.SortOrder))
            .AsQueryable();

        if (activeOnly)
        {
            query = query.Where(x => x.Status == ListingStatus.Active);
        }

        var listings = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        ViewBag.ActiveOnly = activeOnly;

        return View(listings);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(int id, string reason, string? returnUrl = null)
    {
        var reporterId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(reporterId))
        {
            return Challenge();
        }

        var listing = await _context.Listings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (listing is null)
        {
            TempData["ErrorMessage"] = "Şikayet etmek istediğiniz ilan bulunamadı.";
            return RedirectToAction(nameof(Index));
        }

        if (listing.OwnerUserId == reporterId)
        {
            TempData["ErrorMessage"] = "Kendi ilanınızı şikayet edemezsiniz.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var cleanedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(cleanedReason))
        {
            TempData["ErrorMessage"] = "Lütfen şikayet nedeninizi yazınız.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (cleanedReason.Length > 500)
        {
            TempData["ErrorMessage"] = "Şikayet metni en fazla 500 karakter olabilir.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var alreadyReported = await _context.Reports
            .AsNoTracking()
            .AnyAsync(x => x.ReporterId == reporterId && x.ReportedListingId == id);

        if (alreadyReported)
        {
            TempData["ErrorMessage"] = "Bu ilan için zaten şikayet oluşturdunuz.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        var report = new Report
        {
            ReporterId = reporterId,
            ReportedListingId = id,
            Reason = cleanedReason,
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Şikayetiniz belediye yönetimine iletildi.";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private bool IsOwner(Listing listing)
    {
        return listing.OwnerUserId == GetCurrentUserId();
    }

    private bool CanManageListing()
    {
        return User.IsInRole("Admin");
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private async Task<(List<string> PhotoPaths, string? Error)> SavePhotosAsync(IReadOnlyCollection<IFormFile>? photos)
    {
        var savedPaths = new List<string>();
        if (photos is null || photos.Count == 0)
        {
            return (savedPaths, null);
        }

        if (photos.Count > MaxPhotoCount)
        {
            return (savedPaths, $"En fazla {MaxPhotoCount} fotoğraf yükleyebilirsin.");
        }

        var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "listings");
        Directory.CreateDirectory(uploadsPath);

        foreach (var photo in photos.Where(x => x.Length > 0))
        {
            if (photo.Length > MaxPhotoSizeBytes)
            {
                return (new List<string>(), "Her fotoğraf en fazla 5 MB olabilir.");
            }

            var extension = Path.GetExtension(photo.FileName);
            if (string.IsNullOrWhiteSpace(extension) ||
                !AllowedPhotoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return (new List<string>(), "Sadece JPG, PNG ve WEBP dosyaları yükleyebilirsin.");
            }

            if (string.IsNullOrWhiteSpace(photo.ContentType) ||
                !AllowedPhotoContentTypes.Contains(photo.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                return (new List<string>(), "Geçersiz dosya tipi tespit edildi.");
            }

            var fileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(uploadsPath, fileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await photo.CopyToAsync(stream);

            savedPaths.Add($"/uploads/listings/{fileName}");
        }

        return (savedPaths, null);
    }

    private void DeletePhotoFile(string? photoPath)
    {
        if (string.IsNullOrWhiteSpace(photoPath))
        {
            return;
        }

        var relativePath = photoPath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, relativePath));
        var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads", "listings"));

        if (!fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
        {
            return;
        }

        System.IO.File.Delete(fullPath);
    }

    private static DateTime NormalizeExpirationDateToUtc(DateTime input)
    {
        var dateOnly = input.Date;

        return input.Kind == DateTimeKind.Utc
            ? dateOnly
            : DateTime.SpecifyKind(dateOnly, DateTimeKind.Utc);
    }

    private static string? NormalizeText(string? input)
    {
        return string.IsNullOrWhiteSpace(input) ? null : input.Trim();
    }

    private async Task NotifySubscribersForNewListingAsync(Listing listing, string ownerUserId)
    {
        var subscribers = await _context.Users
            .AsNoTracking()
            .Where(x => x.NotifyOnNewListingsByEmail && x.EmailConfirmedAt.HasValue && x.Id != ownerUserId)
            .Select(x => x.Id)
            .ToListAsync();

        if (subscribers.Count == 0)
        {
            return;
        }

        var message = $"Yeni kıyafet ilanı eklendi: {listing.Title} - {listing.Location}";
        foreach (var subscriberId in subscribers)
        {
            await _notificationService.CreateNotificationAsync(
                subscriberId,
                "Sisteme Yeni Kıyafet İlanı Eklendi",
                message,
                NotificationType.NewListingPublished,
                listing.Id,
                "Listing");
        }
    }
}
