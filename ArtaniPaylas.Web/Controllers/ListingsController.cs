using System.Security.Claims;
using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.Enums;
using ArtaniPaylas.Core.Interfaces;
using ArtaniPaylas.Core.ViewModels;
using ArtaniPaylas.Data;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    private readonly UserManager<ApplicationUser> _userManager;

    public ListingsController(
        ApplicationDbContext context,
        IListingStatusService listingStatusService,
        IWebHostEnvironment environment,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _listingStatusService = listingStatusService;
        _environment = environment;
        _userManager = userManager;
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
    public async Task<IActionResult> Create()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser is null)
        {
            return Challenge();
        }

        if (string.IsNullOrWhiteSpace(currentUser.ProfileImageUrl))
        {
            TempData["ErrorMessage"] = "�lan olu�turmadan �nce profil foto�raf� eklemen gerekiyor.";
            return RedirectToAction("Edit", "Profile");
        }

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

        var currentUser = await _userManager.FindByIdAsync(userId);
        if (currentUser is null)
        {
            return Challenge();
        }

        if (string.IsNullOrWhiteSpace(currentUser.ProfileImageUrl))
        {
            TempData["ErrorMessage"] = "�lan olu�turmadan �nce profil foto�raf� eklemen gerekiyor.";
            return RedirectToAction("Edit", "Profile");
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
            TempData["ErrorMessage"] = "Kapanan veya tamamlanan ilanlar d�zenlenemez.";
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
            TempData["ErrorMessage"] = "Kapanan veya tamamlanan ilanlar d�zenlenemez.";
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

        TempData["SuccessMessage"] = "�lan g�ncellendi.";
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

        TempData["SuccessMessage"] = "�lan silindi.";
        return RedirectToAction(nameof(MyListings));
    }

    [Authorize]
    public async Task<IActionResult> MyListings()
    {
        await _listingStatusService.UpdateExpiredListingsAsync();

        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var listings = await _context.Listings
            .Where(x => x.OwnerUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return View(listings);
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
            return (null, "Foto�raf en fazla 5 MB olabilir.");
        }

        var uploadsPath = Path.Combine(_environment.WebRootPath, "uploads", "listings");
        Directory.CreateDirectory(uploadsPath);

        var extension = Path.GetExtension(photo.FileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedPhotoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return (null, "Sadece JPG, PNG ve WEBP dosyalar� y�kleyebilirsin.");
        }

        if (string.IsNullOrWhiteSpace(photo.ContentType) ||
            !AllowedPhotoContentTypes.Contains(photo.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return (null, "Ge�ersiz dosya tipi tespit edildi.");
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
