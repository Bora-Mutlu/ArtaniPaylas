using System.Security.Claims;
using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.Enums;
using ArtaniPaylas.Core.ViewModels;
using ArtaniPaylas.Data;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

namespace ArtaniPaylas.Web.Controllers;

[Authorize]
public class ReviewsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReviewsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Create(int requestId)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var request = await _context.Requests
            .Include(x => x.RequesterUser)
            .Include(x => x.Listing)
            .ThenInclude(x => x!.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == requestId);

        if (request is null || request.Listing is null)
        {
            return NotFound();
        }

        if (request.Status != RequestStatus.Delivered)
        {
            TempData["ErrorMessage"] = "Sadece teslim edilen talepler de�erlendirilebilir.";
            return RedirectToAction("Index", "Profile");
        }

        var targetUser = GetTargetUser(request, userId);
        if (targetUser is null)
        {
            return Forbid();
        }

        var alreadyReviewed = await _context.Reviews.AnyAsync(x => x.RequestId == requestId && x.FromUserId == userId);
        if (alreadyReviewed)
        {
            TempData["ErrorMessage"] = "Bu teslim i�in zaten de�erlendirme yapt�n.";
            return RedirectToAction("Index", "Profile");
        }

        var model = new ReviewCreateViewModel
        {
            RequestId = requestId,
            TargetUserDisplayName = targetUser.FullName ?? targetUser.UserName ?? "Kullan�c�"
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReviewCreateViewModel model)
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

        var request = await _context.Requests
            .Include(x => x.RequesterUser)
            .Include(x => x.Listing)
            .ThenInclude(x => x!.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == model.RequestId);

        if (request is null || request.Listing is null)
        {
            return NotFound();
        }

        if (request.Status != RequestStatus.Delivered)
        {
            TempData["ErrorMessage"] = "Bu talep teslim edilmedi�i i�in de�erlendirme yap�lamaz.";
            return RedirectToAction("Index", "Profile");
        }

        var targetUser = GetTargetUser(request, userId);
        if (targetUser is null)
        {
            return Forbid();
        }

        var alreadyReviewed = await _context.Reviews.AnyAsync(x => x.RequestId == model.RequestId && x.FromUserId == userId);
        if (alreadyReviewed)
        {
            TempData["ErrorMessage"] = "Bu teslim i�in zaten de�erlendirme yapt�n.";
            return RedirectToAction("Index", "Profile");
        }

        var review = new Review
        {
            RequestId = model.RequestId,
            FromUserId = userId,
            ToUserId = targetUser.Id,
            Rating = model.Rating,
            Comment = model.Comment,
            CreatedAt = DateTime.UtcNow
        };

        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        var averageScore = await _context.Reviews
            .Where(x => x.ToUserId == targetUser.Id)
            .AverageAsync(x => (double)x.Rating);

        targetUser.TrustScore = Math.Round(averageScore, 2);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Değerlendirmen kaydedildi.";
        return RedirectToAction("History", "Profile");
    }

    private static ApplicationUser? GetTargetUser(Request request, string currentUserId)
    {
        if (request.RequesterUserId == currentUserId)
        {
            return request.Listing?.OwnerUser;
        }

        if (request.Listing?.OwnerUserId == currentUserId)
        {
            return request.RequesterUser;
        }

        return null;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
