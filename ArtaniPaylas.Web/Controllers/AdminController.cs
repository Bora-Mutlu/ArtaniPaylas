using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.Enums;
using ArtaniPaylas.Core.Extensions;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using ArtaniPaylas.Web.Services;

namespace ArtaniPaylas.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ArtaniPaylas.Data.ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ArtaniPaylas.Core.Interfaces.INotificationService _notificationService;
    private readonly ArtaniPaylas.Core.Interfaces.IEmailService _emailService;

    public AdminController(
        ArtaniPaylas.Data.ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ArtaniPaylas.Core.Interfaces.INotificationService notificationService,
        ArtaniPaylas.Core.Interfaces.IEmailService emailService)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
        _emailService = emailService;
    }

    public async Task<IActionResult> Index()
    {
        var nowUtc = DateTime.UtcNow;
        var sevenDaysAgo = nowUtc.AddDays(-7);

        var totalUsers = await _context.Users.CountAsync();
        var totalListings = await _context.Listings.CountAsync();
        var activeListings = await _context.Listings.CountAsync(x => x.Status == ListingStatus.Active);
        var listingsLast7Days = await _context.Listings.CountAsync(x => x.CreatedAt >= sevenDaysAgo);

        var pendingReports = await _context.Reports.CountAsync(x => x.Status == ReportStatus.Pending);
        var resolvedReports = await _context.Reports.CountAsync(x => x.Status == ReportStatus.Resolved);

        var latestListings = await _context.Listings
            .OrderByDescending(x => x.CreatedAt)
            .Take(6)
            .Select(x => new ActivityItem
            {
                CreatedAt = x.CreatedAt,
                Text = $"Yeni ilan: {x.Title}"
            })
            .ToListAsync();

        var latestReports = await _context.Reports
            .OrderByDescending(x => x.CreatedAt)
            .Take(6)
            .Select(x => new ActivityItem
            {
                CreatedAt = x.CreatedAt,
                Text = x.ReportedListingId != null
                    ? $"İlan raporu alındı (No: {x.ReportedListingId})"
                    : "Kullanıcı/Genel rapor alındı"
            })
            .ToListAsync();

        var recentActivities = latestListings
            .Concat(latestReports)
            .OrderByDescending(x => x.CreatedAt)
            .Take(6)
            .Select(x => $"{x.CreatedAt.ToLocalTime():dd.MM.yyyy HH:mm} - {x.Text}")
            .ToList();
        
        ViewBag.TotalUsers = totalUsers;
        ViewBag.TotalListings = totalListings;
        ViewBag.ActiveListings = activeListings;
        ViewBag.ListingsLast7Days = listingsLast7Days;
        ViewBag.PendingReports = pendingReports;
        ViewBag.ResolvedReports = resolvedReports;
        ViewBag.RecentActivities = recentActivities;

        return View();
    }

    // 1. Kullanıcı Yönetimi
    public async Task<IActionResult> Users()
    {
        var currentUserId = _userManager.GetUserId(User);
        var users = await _context.Users
            .Where(x => x.Id != currentUserId)
            .ToListAsync();
        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserStatus(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user != null)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (string.Equals(user.Id, currentUserId, StringComparison.Ordinal))
            {
                TempData["ErrorMessage"] = "Kendi hesabınızı bu ekrandan pasife alamazsınız.";
                return RedirectToAction(nameof(Users));
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
        }
        return RedirectToAction(nameof(Users));
    }

    // 2. İlan Moderasyonu
    public async Task<IActionResult> Listings()
    {
        var listings = await _context.Listings.Include(x => x.OwnerUser).OrderByDescending(x => x.CreatedAt).ToListAsync();
        return View(listings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModerateListing(int id, ListingStatus status, string? returnUrl = null)
    {
        var listing = await _context.Listings.FindAsync(id);
        if (listing != null)
        {
            listing.Status = status;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "İlan durumu güncellendi.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Listings));
    }

    // 3. Şikayet / Rapor Yönetimi
    public async Task<IActionResult> Reports()
    {
        var reports = await _context.Reports
            .Include(x => x.Reporter)
            .Include(x => x.ReportedUser)
            .Include(x => x.ReportedListing)
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();
        return View(reports);
    }

    [HttpGet("/Admin/Reports/Review/{reportId:int}")]
    public async Task<IActionResult> ListingReportReview(int reportId)
    {
        var report = await _context.Reports
            .Include(x => x.Reporter)
            .Include(x => x.ReportedListing)
            .ThenInclude(x => x!.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == reportId);

        if (report is null)
        {
            return NotFound();
        }

        if (report.ReportedListingId is null || report.ReportedListing is null)
        {
            TempData["ErrorMessage"] = "Bu rapor bir ilana bağlı değil.";
            return RedirectToAction(nameof(Reports));
        }

        return View(report);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReportStatus(int id, ReportStatus status, ReportStatus? expectedCurrentStatus = null, string? returnUrl = null)
    {
        var report = await _context.Reports.FindAsync(id);
        if (report is null)
        {
            TempData["ErrorMessage"] = "Rapor bulunamadı.";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Reports));
        }

        if (expectedCurrentStatus.HasValue && report.Status != expectedCurrentStatus.Value)
        {
            TempData["ErrorMessage"] = "Rapor durumu başka bir işlemle değişti. Lütfen sayfayı yenileyip tekrar deneyin.";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Reports));
        }

        if (report.Status == status)
        {
            TempData["SuccessMessage"] = "Rapor zaten seçilen durumda.";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Reports));
        }

        if (!IsValidReportStatusTransition(report.Status, status))
        {
            TempData["ErrorMessage"] = $"{report.Status.ToDisplayText()} durumundaki rapor {status.ToDisplayText()} durumuna geçirilemez.";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Reports));
        }

        report.Status = status;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = status == ReportStatus.Dismissed
            ? "Rapor reddedildi."
            : $"Rapor durumu {status.ToDisplayText()} olarak güncellendi.";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Reports));
    }

    public IActionResult Settings()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyReviewDecision(
        int reportId,
        ListingStatus listingStatus,
        ReportStatus reportStatus,
        string? adminNote,
        string? returnUrl = null)
    {
        var report = await _context.Reports
            .Include(x => x.Reporter)
            .Include(x => x.ReportedListing)
            .ThenInclude(x => x!.OwnerUser)
            .FirstOrDefaultAsync(x => x.Id == reportId);

        if (report is null)
        {
            TempData["ErrorMessage"] = "Rapor bulunamadı.";
            return RedirectToAction(nameof(Reports));
        }

        if (!IsValidReportStatusTransition(report.Status, reportStatus) && report.Status != reportStatus)
        {
            TempData["ErrorMessage"] = $"{report.Status.ToDisplayText()} durumundaki rapor {reportStatus.ToDisplayText()} durumuna geçirilemez.";
            return RedirectToAction(nameof(ListingReportReview), new { reportId });
        }

        if (reportStatus == ReportStatus.Dismissed && string.IsNullOrWhiteSpace(adminNote))
        {
            TempData["ErrorMessage"] = "Rapor reddi için açıklama zorunludur.";
            return RedirectToAction(nameof(ListingReportReview), new { reportId });
        }

        var listing = report.ReportedListing;
        var oldListingStatus = listing?.Status;

        report.Status = reportStatus;
        if (listing is not null)
        {
            listing.Status = listingStatus;
        }

        await _context.SaveChangesAsync();

        await NotifyReporterForDecisionAsync(report, reportStatus, adminNote);
        await NotifyListingOwnerForListingActionAsync(report, oldListingStatus, listingStatus, adminNote);

        TempData["SuccessMessage"] = "Karar kaydedildi ve bilgilendirmeler gönderildi.";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Reports));
    }

    private static bool IsValidReportStatusTransition(ReportStatus currentStatus, ReportStatus targetStatus)
    {
        return currentStatus switch
        {
            ReportStatus.Pending => targetStatus is ReportStatus.Reviewed or ReportStatus.Resolved or ReportStatus.Dismissed,
            ReportStatus.Reviewed => targetStatus is ReportStatus.Pending or ReportStatus.Resolved or ReportStatus.Dismissed,
            ReportStatus.Resolved => targetStatus is ReportStatus.Pending or ReportStatus.Dismissed,
            ReportStatus.Dismissed => targetStatus is ReportStatus.Pending or ReportStatus.Reviewed,
            _ => false
        };
    }

    private async Task NotifyReporterForDecisionAsync(Report report, ReportStatus reportStatus, string? adminNote)
    {
        if (report.Reporter is null || string.IsNullOrWhiteSpace(report.Reporter.Email))
        {
            return;
        }

        var safeNote = string.IsNullOrWhiteSpace(adminNote) ? "Ek açıklama paylaşılmadı." : adminNote.Trim();

        if (reportStatus == ReportStatus.Dismissed)
        {
            var title = "Raporunuz Reddedildi";
            var message = $"Raporunuz incelendi ve reddedildi. Gerekçe: {safeNote}";

            await _notificationService.CreateNotificationAsync(
                report.ReporterId,
                title,
                message,
                NotificationType.SystemNotification,
                report.Id,
                "Report");

            var emailBody = EmailTemplateBuilder.BuildActionEmailTemplate(
                title,
                "Rapor Değerlendirmesi",
                report.Reporter.FullName ?? report.Reporter.UserName ?? "Kullanıcı",
                message);

            await _emailService.SendEmailAsync(report.Reporter.Email, $"ArtaniPaylas - {title}", emailBody);
        }
    }

    private async Task NotifyListingOwnerForListingActionAsync(Report report, ListingStatus? oldStatus, ListingStatus newStatus, string? adminNote)
    {
        var listing = report.ReportedListing;
        var owner = listing?.OwnerUser;
        if (listing is null || owner is null || string.IsNullOrWhiteSpace(owner.Email))
        {
            return;
        }

        if (oldStatus == newStatus)
        {
            return;
        }

        if (newStatus != ListingStatus.Canceled && newStatus != ListingStatus.Suspended)
        {
            return;
        }

        var detailLink = Url.Action("Details", "Listings", new { id = listing.Id }, Request.Scheme) ?? string.Empty;
        var title = "Ilaniniz Moderasyon Nedeniyle Kaldirildi";
        var safeNote = string.IsNullOrWhiteSpace(adminNote) ? "Moderasyon politikası gereği işlem yapıldı." : adminNote.Trim();
        var message = $"'{listing.Title}' ilanınız {newStatus.ToDisplayText()} durumuna alındı. Detay: {safeNote}";

        await _notificationService.CreateNotificationAsync(
            owner.Id,
            title,
            message,
            NotificationType.SystemNotification,
            listing.Id,
            "Listing");

        var emailBody = EmailTemplateBuilder.BuildActionEmailTemplate(
            title,
            "İlan Moderasyon Güncellemesi",
            owner.FullName ?? owner.UserName ?? "Kullanıcı",
            message,
            detailLink,
            "İlan Detayını Aç");

        await _emailService.SendEmailAsync(owner.Email, $"ArtaniPaylas - {title}", emailBody);
    }

    private sealed class ActivityItem
    {
        public DateTime CreatedAt { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}



