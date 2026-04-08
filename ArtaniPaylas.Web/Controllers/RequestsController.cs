using System.Security.Claims;
using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.Enums;
using ArtaniPaylas.Data;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

namespace ArtaniPaylas.Web.Controllers;

[Authorize]
public class RequestsController : Controller
{
    private readonly ApplicationDbContext _context;

    public RequestsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int listingId)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var listing = await _context.Listings
            .Include(x => x.Requests)
            .FirstOrDefaultAsync(x => x.Id == listingId);

        if (listing is null)
        {
            return NotFound();
        }

        if (listing.OwnerUserId == userId)
        {
            TempData["ErrorMessage"] = "Kendi ilan�na talep g�nderemezsin.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }

        if (listing.Status != ListingStatus.Active)
        {
            TempData["ErrorMessage"] = "Bu ilana talep al�m� kapal�.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }

        var existingRequest = listing.Requests.Any(x => x.RequesterUserId == userId && x.Status == RequestStatus.Pending);
        if (existingRequest)
        {
            TempData["ErrorMessage"] = "Bu ilan i�in zaten bekleyen bir talebin var.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }

        var request = new Request
        {
            ListingId = listing.Id,
            RequesterUserId = userId,
            Status = RequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Requests.Add(request);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Talebin gönderildi.";
        return RedirectToAction(nameof(Outgoing));
    }

    public async Task<IActionResult> Incoming()
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var requests = await _context.Requests
            .Include(x => x.RequesterUser)
            .Include(x => x.Listing)
            .Where(x => x.Listing != null && x.Listing.OwnerUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return View(requests);
    }

    public async Task<IActionResult> Outgoing()
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var requests = await _context.Requests
            .Include(x => x.Listing)
            .ThenInclude(x => x!.OwnerUser)
            .Where(x => x.RequesterUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return View(requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        return await UpdateRequestStatusAsync(id, RequestStatus.Approved);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        return await UpdateRequestStatusAsync(id, RequestStatus.Rejected);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDelivered(int id)
    {
        return await UpdateRequestStatusAsync(id, RequestStatus.Delivered);
    }

    private async Task<IActionResult> UpdateRequestStatusAsync(int requestId, RequestStatus targetStatus)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var request = await _context.Requests
            .Include(x => x.Listing)
            .FirstOrDefaultAsync(x => x.Id == requestId);

        if (request is null || request.Listing is null)
        {
            return NotFound();
        }

        if (request.Listing.OwnerUserId != userId)
        {
            return Forbid();
        }

        request.Status = targetStatus;
        request.UpdatedAt = DateTime.UtcNow;

        if (targetStatus is RequestStatus.Approved or RequestStatus.Delivered)
        {
            var otherPendingRequests = await _context.Requests
                .Where(x => x.ListingId == request.ListingId && x.Id != request.Id && x.Status == RequestStatus.Pending)
                .ToListAsync();

            foreach (var pendingRequest in otherPendingRequests)
            {
                pendingRequest.Status = RequestStatus.Rejected;
                pendingRequest.UpdatedAt = DateTime.UtcNow;
            }

            request.Listing.Status = ListingStatus.Completed;
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Talep durumu güncellendi.";
        return RedirectToAction(nameof(Incoming));
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
