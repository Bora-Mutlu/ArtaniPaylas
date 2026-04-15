using System.Security.Claims;
using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.Enums;
using ArtaniPaylas.Core.Extensions;
using ArtaniPaylas.Core.ViewModels;
using ArtaniPaylas.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtaniPaylas.Web.Controllers;

[Authorize]
public class UserDashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public UserDashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var userInfo = await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new
            {
                x.FullName,
                x.UserName,
                x.ProfileImageUrl
            })
            .FirstOrDefaultAsync();

        var nowUtc = DateTime.UtcNow;
        var sevenDaysAgo = nowUtc.AddDays(-7);

        var activeListingsCount = await _context.Listings
            .CountAsync(x => x.OwnerUserId == userId && x.Status == ListingStatus.Active);

        var newListingsLast7Days = await _context.Listings
            .CountAsync(x => x.OwnerUserId == userId && x.CreatedAt >= sevenDaysAgo);

        var incomingRequestsCount = await _context.Requests
            .CountAsync(x => x.Listing != null && x.Listing.OwnerUserId == userId);

        var pendingIncomingRequestsCount = await _context.Requests
            .CountAsync(x => x.Listing != null && x.Listing.OwnerUserId == userId && x.Status == RequestStatus.Pending);

        var totalOutgoingRequests = await _context.Requests
            .CountAsync(x => x.RequesterUserId == userId);

        var successfulOutgoingRequests = await _context.Requests
            .CountAsync(x => x.RequesterUserId == userId && (x.Status == RequestStatus.Approved || x.Status == RequestStatus.Delivered));

        var outgoingSuccessRate = totalOutgoingRequests == 0
            ? 0d
            : Math.Round((double)successfulOutgoingRequests / totalOutgoingRequests * 100, 1);

        var recentListingsData = await _context.Listings
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new
            {
                Id = x.Id,
                Title = x.Title,
                PhotoPath = x.PhotoPath,
                Status = x.Status,
                IsEditable = x.Status == ListingStatus.Active,
                RequestCount = x.Requests.Count,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync();

        var recentListings = recentListingsData
            .Select(x => new UserDashboardListingItemViewModel
            {
                Id = x.Id,
                Title = x.Title,
                PhotoPath = x.PhotoPath,
                StatusText = x.Status.ToDisplayText(),
                IsEditable = x.IsEditable,
                RequestCount = x.RequestCount,
                CreatedAt = x.CreatedAt
            })
            .ToList();

        var pendingIncomingRequests = await _context.Requests
            .AsNoTracking()
            .Where(x => x.Listing != null && x.Listing.OwnerUserId == userId && x.Status == RequestStatus.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new
            {
                x.Id,
                RequesterName = x.RequesterUser != null
                    ? (x.RequesterUser.FullName ?? x.RequesterUser.UserName ?? "Kullanıcı")
                    : "Kullanıcı",
                ListingTitle = x.Listing != null ? x.Listing.Title : "İlan",
                x.CreatedAt
            })
            .ToListAsync();

        var pendingItems = pendingIncomingRequests
            .Select(x => new UserDashboardPendingRequestItemViewModel
            {
                RequestId = x.Id,
                RequesterName = x.RequesterName,
                RequesterInitials = GetInitials(x.RequesterName),
                ListingTitle = x.ListingTitle,
                CreatedAt = x.CreatedAt
            })
            .ToList();

        var model = new UserDashboardViewModel
        {
            DisplayName = userInfo?.FullName ?? userInfo?.UserName ?? "Kullanıcı",
            ProfileImageUrl = userInfo?.ProfileImageUrl,
            ActiveListingsCount = activeListingsCount,
            NewListingsLast7Days = newListingsLast7Days,
            IncomingRequestsCount = incomingRequestsCount,
            PendingIncomingRequestsCount = pendingIncomingRequestsCount,
            OutgoingSuccessRate = outgoingSuccessRate,
            RecentListings = recentListings,
            PendingIncomingRequests = pendingItems
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReport(string reason)
    {
        var reporterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(reporterId))
        {
            return Challenge();
        }

        var cleanedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(cleanedReason))
        {
            TempData["ErrorMessage"] = "Lütfen rapor açıklaması giriniz.";
            return RedirectToAction(nameof(Index));
        }

        if (cleanedReason.Length > 500)
        {
            TempData["ErrorMessage"] = "Rapor açıklaması en fazla 500 karakter olabilir.";
            return RedirectToAction(nameof(Index));
        }

        var report = new Report
        {
            ReporterId = reporterId,
            Reason = cleanedReason
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Raporunuz admin ekibine iletildi.";
        return RedirectToAction(nameof(Index));
    }

    private static string GetInitials(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "AP";
        }

        var parts = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .ToArray();

        if (parts.Length == 0)
        {
            return "AP";
        }

        if (parts.Length == 1)
        {
            return char.ToUpperInvariant(parts[0][0]).ToString();
        }

        return string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[1][0]));
    }
}
