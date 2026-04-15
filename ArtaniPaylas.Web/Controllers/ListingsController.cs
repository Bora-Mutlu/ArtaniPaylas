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

    private readonly ApplicationDbContext _context;
    private readonly IListingStatusService _listingStatusService;
    private readonly IWebHostEnvironment _environment;

    public ListingsController(
        ApplicationDbContext context,
        IListingStatusService listingStatusService,
        IWebHostEnvironment environment)
    {
        _context = context;
        _listingStatusService = listingStatusService;
        _environment = environment;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(ListingsFilterViewModel filter)
    {
        await _listingStatusService.UpdateExpiredListingsAsync();

        var query = _context.Listings
            .Include(x => x.OwnerUser)
            .AsQueryable();

        if (filter.ActiveOnly)
        {
            query = query.Where(x => x.Status == ListingStatus.Active);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTitle))
        {
            var searchTitle = filter.SearchTitle.Trim();
            query = query.Where(x => x.Title.Contains(searchTitle));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchLocation))
        {
            var searchLocation = filter.SearchLocation.Trim();
            query = query.Where(x => x.Location.Contains(searchLocation));
        }

        var listings = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var model = new ListingsIndexViewModel
        {
            Filter = filter,
            Listings = listings
        };

        return View(model);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        await _listingStatusService.UpdateExpiredListingsAsync();

        var listing = await _context.Listings
            .Include(x => x.OwnerUser)
            .Include(x => x.Requests)
            .ThenInclude(x => x.RequesterUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (listing is null)
        {
            return NotFound();
        }

        return View(listing);
    }

    [Authorize]
    public IActionResult Create()
    {
        return View(new ListingCreateEditViewModel());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ListingCreateEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var listing = new Listing
        {
            OwnerUserId = userId,
            Title = model.Title,
            Description = model.Description,
            ExpirationDate = NormalizeExpirationDateToUtc(model.ExpirationDate),
            Location = model.Location,
            Status = ListingStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var (photoPath, photoError) = await SavePhotoAsync(model.Photo);
        if (!string.IsNullOrWhiteSpace(photoError))
        {
            ModelState.AddModelError(nameof(model.Photo), photoError);
            return View(model);
        }

        listing.PhotoPath = photoPath;

        _context.Listings.Add(listing);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "İlan oluşturuldu.";
        return RedirectToAction(nameof(MyListings));
    }

    [Authorize]
    public async Task<IActionResult> Edit(int id)
    {
        var listing = await _context.Listings.FirstOrDefaultAsync(x => x.Id == id);
        if (listing is null)
        {
            return NotFound();
        }

        if (!IsOwner(listing))
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
            ExpirationDate = listing.ExpirationDate,
            Location = listing.Location,
            ExistingPhotoPath = listing.PhotoPath
        };

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ListingCreateEditViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        var listing = await _context.Listings.FirstOrDefaultAsync(x => x.Id == id);
        if (listing is null)
        {
            return NotFound();
        }

        if (!IsOwner(listing))
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
            model.ExistingPhotoPath = listing.PhotoPath;
            return View(model);
        }

        listing.Title = model.Title;
        listing.Description = model.Description;
        listing.ExpirationDate = NormalizeExpirationDateToUtc(model.ExpirationDate);
        listing.Location = model.Location;

        var (newPhotoPath, photoError) = await SavePhotoAsync(model.Photo);
        if (!string.IsNullOrWhiteSpace(photoError))
        {
            ModelState.AddModelError(nameof(model.Photo), photoError);
            model.ExistingPhotoPath = listing.PhotoPath;
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(newPhotoPath))
        {
            listing.PhotoPath = newPhotoPath;
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "İlan güncellendi.";
        return RedirectToAction(nameof(MyListings));
    }

    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        var listing = await _context.Listings.FirstOrDefaultAsync(x => x.Id == id);
        if (listing is null)
        {
            return NotFound();
        }

        if (!IsOwner(listing))
        {
            return Forbid();
        }

        return View(listing);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var listing = await _context.Listings.FirstOrDefaultAsync(x => x.Id == id);
        if (listing is null)
        {
            return NotFound();
        }

        if (!IsOwner(listing))
        {
            return Forbid();
        }

        _context.Listings.Remove(listing);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "İlan silindi.";
        return RedirectToAction(nameof(MyListings));
    }

    [Authorize]
    public async Task<IActionResult> MyListings(bool activeOnly = false)
    {
        await _listingStatusService.UpdateExpiredListingsAsync();

        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var query = _context.Listings
            .Where(x => x.OwnerUserId == userId);

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

        TempData["SuccessMessage"] = "Şikayetiniz admin ekibine iletildi.";

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

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private async Task<(string? PhotoPath, string? Error)> SavePhotoAsync(IFormFile? photo)
    {
        if (photo is null || photo.Length == 0)
        {
            return (null, null);
        }

        if (photo.Length > MaxPhotoSizeBytes)
        {
            return (null, "Fotoğraf en fazla 5 MB olabilir.");
        }

        var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "listings");
        Directory.CreateDirectory(uploadsPath);

        var extension = Path.GetExtension(photo.FileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedPhotoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return (null, "Sadece JPG, PNG ve WEBP dosyaları yükleyebilirsin.");
        }

        if (string.IsNullOrWhiteSpace(photo.ContentType) ||
            !AllowedPhotoContentTypes.Contains(photo.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return (null, "Geçersiz dosya tipi tespit edildi.");
        }

        var fileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(uploadsPath, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await photo.CopyToAsync(stream);

        return ($"/uploads/listings/{fileName}", null);
    }

    private static DateTime NormalizeExpirationDateToUtc(DateTime input)
    {
        var dateOnly = input.Date;

        return input.Kind == DateTimeKind.Utc
            ? dateOnly
            : DateTime.SpecifyKind(dateOnly, DateTimeKind.Utc);
    }
}
