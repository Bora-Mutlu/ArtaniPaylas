using System.Text.Json;
using System.Text.Json.Serialization;
using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.Enums;
using ArtaniPaylas.Core.Extensions;
using ArtaniPaylas.Core.ViewModels;
using ArtaniPaylas.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtaniPaylas.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ArtaniPaylas.Data.ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ArtaniPaylas.Core.Interfaces.INotificationService _notificationService;
    private readonly ArtaniPaylas.Core.Interfaces.IEmailService _emailService;
    private readonly IWebHostEnvironment _environment;

    public AdminController(
        ArtaniPaylas.Data.ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ArtaniPaylas.Core.Interfaces.INotificationService notificationService,
        ArtaniPaylas.Core.Interfaces.IEmailService emailService,
        IWebHostEnvironment environment)
    {
        _context = context;
        _userManager = userManager;
        _notificationService = notificationService;
        _emailService = emailService;
        _environment = environment;
    }

    public async Task<IActionResult> Index()
    {
        var nowUtc = DateTime.UtcNow;
        var sevenDaysAgo = nowUtc.AddDays(-7);

        var totalUsers = await _context.Users.CountAsync();
        var totalListings = await _context.Listings.CountAsync();
        var activeListings = await _context.Listings.CountAsync(x => x.Status == ListingStatus.Active);
        var listingsLast7Days = await _context.Listings.CountAsync(x => x.CreatedAt >= sevenDaysAgo);
        var totalContainers = await _context.ContainerLocations.CountAsync();
        var activeContainers = await _context.ContainerLocations.CountAsync(x => x.IsActive);
        var pendingAppointments = await _context.Requests.CountAsync(x => x.Status == RequestStatus.Pending);
        var approvedAppointments = await _context.Requests.CountAsync(x => x.Status == RequestStatus.Approved);
        var pendingReports = await _context.Reports.CountAsync(x => x.Status == ReportStatus.Pending);
        var resolvedReports = await _context.Reports.CountAsync(x => x.Status == ReportStatus.Resolved);

        var latestListings = await _context.Listings
            .OrderByDescending(x => x.CreatedAt)
            .Take(4)
            .Select(x => new ActivityItem
            {
                CreatedAt = x.CreatedAt,
                Text = $"Yeni kıyafet ilanı: {x.Title}"
            })
            .ToListAsync();

        var latestRequests = await _context.Requests
            .Include(x => x.Listing)
            .OrderByDescending(x => x.CreatedAt)
            .Take(4)
            .Select(x => new ActivityItem
            {
                CreatedAt = x.CreatedAt,
                Text = $"Randevu talebi: {(x.Listing != null ? x.Listing.Title : "İlan")}"
            })
            .ToListAsync();

        var recentActivities = latestListings
            .Concat(latestRequests)
            .OrderByDescending(x => x.CreatedAt)
            .Take(8)
            .Select(x => $"{x.CreatedAt.ToLocalTime():dd.MM.yyyy HH:mm} - {x.Text}")
            .ToList();

        ViewBag.TotalUsers = totalUsers;
        ViewBag.TotalListings = totalListings;
        ViewBag.ActiveListings = activeListings;
        ViewBag.ListingsLast7Days = listingsLast7Days;
        ViewBag.TotalContainers = totalContainers;
        ViewBag.ActiveContainers = activeContainers;
        ViewBag.PendingAppointments = pendingAppointments;
        ViewBag.ApprovedAppointments = approvedAppointments;
        ViewBag.PendingReports = pendingReports;
        ViewBag.ResolvedReports = resolvedReports;
        ViewBag.RecentActivities = recentActivities;

        return View();
    }

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
                TempData["ErrorMessage"] = "Kendi hesabÃƒâ€Ã‚Â±nÃƒâ€Ã‚Â±zÃƒâ€Ã‚Â± bu ekrandan pasife alamazsÃƒâ€Ã‚Â±nÃƒâ€Ã‚Â±z.";
                return RedirectToAction(nameof(Users));
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
        }
        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> Listings(string? search, string? category, ListingStatus? status, DateTime? createdDate, int page = 1)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);

        var allListings = _context.Listings.AsQueryable();
        var query = _context.Listings
            .Include(x => x.OwnerUser)
            .Include(x => x.Photos.OrderBy(p => p.SortOrder))
            .AsQueryable();

        var cleanSearch = NormalizeText(search);
        if (!string.IsNullOrWhiteSpace(cleanSearch))
        {
            query = query.Where(x => x.Title.Contains(cleanSearch) || x.Description.Contains(cleanSearch));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category == category);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (createdDate.HasValue)
        {
            var dayStart = createdDate.Value.Date;
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(x => x.CreatedAt >= dayStart && x.CreatedAt < dayEnd);
        }

        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (page > totalPages)
        {
            page = totalPages;
        }

        var listings = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.TotalListings = await allListings.CountAsync();
        ViewBag.ActiveListings = await allListings.CountAsync(x => x.Status == ListingStatus.Active);
        ViewBag.SuspendedListings = await allListings.CountAsync(x => x.Status == ListingStatus.Suspended);
        ViewBag.TodayListings = await allListings.CountAsync(x => x.CreatedAt >= DateTime.UtcNow.Date);
        ViewBag.Categories = await allListings
            .Where(x => x.Category != null && x.Category != string.Empty)
            .Select(x => x.Category)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();
        ViewBag.Search = search;
        ViewBag.Category = category;
        ViewBag.Status = status;
        ViewBag.CreatedDate = createdDate;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = totalPages;

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
            TempData["SuccessMessage"] = "Ãƒâ€Ã‚Â°lan durumu gÃƒÆ’Ã‚Â¼ncellendi.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Listings));
    }

    public async Task<IActionResult> Containers()
    {
        var containers = await _context.ContainerLocations
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.District)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var boundaryPath = Path.Combine(_environment.WebRootPath, "data", "ayancik-boundary.geojson");
        var boundaryGeoJson = "{}";
        if (System.IO.File.Exists(boundaryPath))
        {
            var rawBoundary = await System.IO.File.ReadAllTextAsync(boundaryPath);
            boundaryGeoJson = SimplifyBoundaryGeoJson(rawBoundary);
        }

        return View(new AdminContainersViewModel
        {
            Containers = containers,
            BoundaryGeoJson = boundaryGeoJson
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateContainers(string containerName, string? district, string? address, string pointsJson)
    {
        if (string.IsNullOrWhiteSpace(containerName) || string.IsNullOrWhiteSpace(pointsJson))
        {
            TempData["ErrorMessage"] = "Konteyner adÃƒâ€Ã‚Â± ve harita noktalarÃƒâ€Ã‚Â± zorunludur.";
            return RedirectToAction(nameof(Containers));
        }

        List<MapPointDto>? points;
        try
        {
            points = JsonSerializer.Deserialize<List<MapPointDto>>(pointsJson);
        }
        catch (JsonException)
        {
            TempData["ErrorMessage"] = "Harita noktalarÃƒâ€Ã‚Â± ÃƒÆ’Ã‚Â§ÃƒÆ’Ã‚Â¶zÃƒÆ’Ã‚Â¼mlenemedi.";
            return RedirectToAction(nameof(Containers));
        }

        if (points is null || points.Count == 0)
        {
            TempData["ErrorMessage"] = "En az bir konteyner noktasÃƒâ€Ã‚Â± seÃƒÆ’Ã‚Â§melisiniz.";
            return RedirectToAction(nameof(Containers));
        }

        var cleanDistrict = NormalizeText(district);
        var cleanAddress = NormalizeText(address);
        var baseName = containerName.Trim();

        foreach (var point in points)
        {
            _context.ContainerLocations.Add(new ContainerLocation
            {
                Name = points.Count == 1 ? baseName : $"{baseName} #{point.Index + 1}",
                District = cleanDistrict,
                Address = cleanAddress,
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"{points.Count} konteyner noktasÃƒâ€Ã‚Â± kaydedildi.";
        return RedirectToAction(nameof(Containers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleContainerStatus(int id)
    {
        var container = await _context.ContainerLocations.FindAsync(id);
        if (container is null)
        {
            TempData["ErrorMessage"] = "Konteyner bulunamadÃƒâ€Ã‚Â±.";
            return RedirectToAction(nameof(Containers));
        }

        container.IsActive = !container.IsActive;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Konteyner durumu gÃƒÆ’Ã‚Â¼ncellendi.";
        return RedirectToAction(nameof(Containers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteContainer(int id)
    {
        var container = await _context.ContainerLocations.FindAsync(id);
        if (container is null)
        {
            TempData["ErrorMessage"] = "Konteyner bulunamadÃƒâ€Ã‚Â±.";
            return RedirectToAction(nameof(Containers));
        }

        _context.ContainerLocations.Remove(container);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Konteyner kaydÃƒâ€Ã‚Â± silindi.";
        return RedirectToAction(nameof(Containers));
    }

    public async Task<IActionResult> Appointments(RequestStatus? status = null, string? search = null, int page = 1)
    {
        const int pageSize = 20;
        page = Math.Max(1, page);

        var query = _context.Requests
            .Include(x => x.RequesterUser)
            .Include(x => x.Listing)
            .ThenInclude(x => x!.OwnerUser)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var cleanSearch = NormalizeText(search);
        if (!string.IsNullOrWhiteSpace(cleanSearch))
        {
            query = query.Where(x =>
                (x.Listing != null && x.Listing.Title.Contains(cleanSearch)) ||
                (x.RequesterUser != null &&
                    ((x.RequesterUser.FullName != null && x.RequesterUser.FullName.Contains(cleanSearch)) ||
                     (x.RequesterUser.UserName != null && x.RequesterUser.UserName.Contains(cleanSearch)) ||
                     (x.RequesterUser.Email != null && x.RequesterUser.Email.Contains(cleanSearch)))) ||
                (x.Listing != null && x.Listing.OwnerUser != null &&
                    ((x.Listing.OwnerUser.FullName != null && x.Listing.OwnerUser.FullName.Contains(cleanSearch)) ||
                     (x.Listing.OwnerUser.UserName != null && x.Listing.OwnerUser.UserName.Contains(cleanSearch)) ||
                     (x.Listing.OwnerUser.Email != null && x.Listing.OwnerUser.Email.Contains(cleanSearch)))));
        }

        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        if (page > totalPages)
        {
            page = totalPages;
        }

        var appointments = await query
            .OrderBy(x => x.Status)
            .ThenBy(x => x.RequestedAppointmentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.SelectedStatus = status;
        ViewBag.Search = cleanSearch;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;

        return View(appointments);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAppointment(
        int id,
        RequestStatus status,
        DateTime? confirmedAppointmentAt,
        string? municipalityNote)
    {
        if (!Enum.IsDefined(status))
        {
            TempData["ErrorMessage"] = "GeÃƒÂ§ersiz randevu durumu seÃƒÂ§ildi.";
            return RedirectToAction(nameof(Appointments));
        }

        var request = await _context.Requests
            .Include(x => x.Listing)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (request is null)
        {
            TempData["ErrorMessage"] = "Randevu kaydÃ„Â± bulunamadÃ„Â±.";
            return RedirectToAction(nameof(Appointments));
        }

        if (!IsValidAppointmentStatusTransition(request.Status, status))
        {
            TempData["ErrorMessage"] = $"{request.Status.ToDisplayText()} durumundaki randevu {status.ToDisplayText()} durumuna geÃƒÂ§irilemez.";
            return RedirectToAction(nameof(Appointments));
        }

        if (status == RequestStatus.Approved)
        {
            if (!confirmedAppointmentAt.HasValue)
            {
                TempData["ErrorMessage"] = "Onay iÃƒÂ§in uygun tarih ve saat belirlemelisiniz.";
                return RedirectToAction(nameof(Appointments));
            }

            var confirmedUtc = DateTime.SpecifyKind(
                confirmedAppointmentAt.Value,
                DateTimeKind.Local).ToUniversalTime();

            if (confirmedUtc <= DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = "Onaylanacak randevu saati ileri bir tarih olmalÃ„Â±dÃ„Â±r.";
                return RedirectToAction(nameof(Appointments));
            }

            request.ConfirmedAppointmentAt = confirmedUtc;
        }

        if (status == RequestStatus.Delivered && !request.ConfirmedAppointmentAt.HasValue)
        {
            TempData["ErrorMessage"] = "TamamlandÃ„Â± iÃ…Å¸areti iÃƒÂ§in randevunun ÃƒÂ¶nce onaylanmÃ„Â±Ã…Å¸ olmasÃ„Â± gerekir.";
            return RedirectToAction(nameof(Appointments));
        }

        request.Status = status;
        request.UpdatedAt = DateTime.UtcNow;
        request.MunicipalityNote = NormalizeText(municipalityNote);

        if (status == RequestStatus.Rejected)
        {
            request.ConfirmedAppointmentAt = null;
        }

        if (status == RequestStatus.Delivered && request.Listing != null)
        {
            request.Listing.Status = ListingStatus.Completed;
        }

        await _context.SaveChangesAsync();
        await NotifyAppointmentUpdateAsync(request);

        TempData["SuccessMessage"] = "Randevu kaydÃ„Â± gÃƒÂ¼ncellendi.";
        return RedirectToAction(nameof(Appointments));
    }

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
            TempData["ErrorMessage"] = "Bu rapor bir ilana baÃƒâ€Ã…Â¸lÃƒâ€Ã‚Â± deÃƒâ€Ã…Â¸il.";
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
            TempData["ErrorMessage"] = "Rapor bulunamadÃƒâ€Ã‚Â±.";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Reports));
        }

        if (expectedCurrentStatus.HasValue && report.Status != expectedCurrentStatus.Value)
        {
            TempData["ErrorMessage"] = "Rapor durumu baÃƒâ€¦Ã…Â¸ka bir iÃƒâ€¦Ã…Â¸lemle deÃƒâ€Ã…Â¸iÃƒâ€¦Ã…Â¸ti. LÃƒÆ’Ã‚Â¼tfen sayfayÃƒâ€Ã‚Â± yenileyip tekrar deneyin.";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Reports));
        }

        if (report.Status == status)
        {
            TempData["SuccessMessage"] = "Rapor zaten seÃƒÆ’Ã‚Â§ilen durumda.";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Reports));
        }

        if (!IsValidReportStatusTransition(report.Status, status))
        {
            TempData["ErrorMessage"] = $"{report.Status.ToDisplayText()} durumundaki rapor {status.ToDisplayText()} durumuna geÃƒÆ’Ã‚Â§irilemez.";
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
            : $"Rapor durumu {status.ToDisplayText()} olarak gÃƒÆ’Ã‚Â¼ncellendi.";

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
            TempData["ErrorMessage"] = "Rapor bulunamadÃƒâ€Ã‚Â±.";
            return RedirectToAction(nameof(Reports));
        }

        if (!IsValidReportStatusTransition(report.Status, reportStatus) && report.Status != reportStatus)
        {
            TempData["ErrorMessage"] = $"{report.Status.ToDisplayText()} durumundaki rapor {reportStatus.ToDisplayText()} durumuna geÃƒÆ’Ã‚Â§irilemez.";
            return RedirectToAction(nameof(ListingReportReview), new { reportId });
        }

        if (reportStatus == ReportStatus.Dismissed && string.IsNullOrWhiteSpace(adminNote))
        {
            TempData["ErrorMessage"] = "Rapor reddi iÃƒÆ’Ã‚Â§in aÃƒÆ’Ã‚Â§Ãƒâ€Ã‚Â±klama zorunludur.";
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

        TempData["SuccessMessage"] = "Karar kaydedildi ve bilgilendirmeler gÃƒÆ’Ã‚Â¶nderildi.";
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

    private static bool IsValidAppointmentStatusTransition(RequestStatus currentStatus, RequestStatus targetStatus)
    {
        return currentStatus switch
        {
            RequestStatus.Pending => targetStatus is RequestStatus.Approved or RequestStatus.Rejected,
            RequestStatus.Approved => targetStatus == RequestStatus.Delivered,
            _ => false
        };
    }

    private async Task NotifyAppointmentUpdateAsync(Request request)
    {
        var title = request.Status switch
        {
            RequestStatus.Approved => "Randevu Talebiniz OnaylandÃ„Â±",
            RequestStatus.Rejected => "Randevu Talebiniz Reddedildi",
            RequestStatus.Delivered => "Randevunuz TamamlandÃ„Â±",
            _ => "Randevu GÃƒÂ¼ncellemesi"
        };

        var appointmentTime = request.ConfirmedAppointmentAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
            ?? request.RequestedAppointmentAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

        var message = request.Status switch
        {
            RequestStatus.Approved => $"Belediye uygun saat olarak {appointmentTime} zamanÃ„Â±nÃ„Â± belirledi.",
            RequestStatus.Rejected => $"Randevu talebiniz uygun bulunmadÃ„Â±. Not: {request.MunicipalityNote ?? "Not paylaÃ…Å¸Ã„Â±lmadÃ„Â±."}",
            RequestStatus.Delivered => "Randevu sÃƒÂ¼reciniz tamamlandÃ„Â±.",
            _ => "Randevu kaydÃ„Â±nÃ„Â±z gÃƒÂ¼ncellendi."
        };

        await _notificationService.CreateNotificationAsync(
            request.RequesterUserId,
            title,
            message,
            request.Status == RequestStatus.Rejected ? NotificationType.RequestRejected : NotificationType.RequestApproved,
            request.Id,
            "Request");
    }

    private async Task NotifyReporterForDecisionAsync(Report report, ReportStatus reportStatus, string? adminNote)
    {
        if (report.Reporter is null || string.IsNullOrWhiteSpace(report.Reporter.Email))
        {
            return;
        }

        var safeNote = string.IsNullOrWhiteSpace(adminNote) ? "Ek aÃƒÆ’Ã‚Â§Ãƒâ€Ã‚Â±klama paylaÃƒâ€¦Ã…Â¸Ãƒâ€Ã‚Â±lmadÃƒâ€Ã‚Â±." : adminNote.Trim();

        if (reportStatus == ReportStatus.Dismissed)
        {
            var title = "Raporunuz Reddedildi";
            var message = $"Raporunuz incelendi ve reddedildi. GerekÃƒÆ’Ã‚Â§e: {safeNote}";

            await _notificationService.CreateNotificationAsync(
                report.ReporterId,
                title,
                message,
                NotificationType.SystemNotification,
                report.Id,
                "Report");

            var emailBody = EmailTemplateBuilder.BuildActionEmailTemplate(
                title,
                "Rapor DeÃƒâ€Ã…Â¸erlendirmesi",
                report.Reporter.FullName ?? report.Reporter.UserName ?? "KullanÃƒâ€Ã‚Â±cÃƒâ€Ã‚Â±",
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
        var title = "Ãƒâ€Ã‚Â°lanÃƒâ€Ã‚Â±nÃƒâ€Ã‚Â±z Moderasyon Nedeniyle KaldÃƒâ€Ã‚Â±rÃƒâ€Ã‚Â±ldÃƒâ€Ã‚Â±";
        var safeNote = string.IsNullOrWhiteSpace(adminNote) ? "Moderasyon politikasÃƒâ€Ã‚Â± gereÃƒâ€Ã…Â¸i iÃƒâ€¦Ã…Â¸lem yapÃƒâ€Ã‚Â±ldÃƒâ€Ã‚Â±." : adminNote.Trim();
        var message = $"'{listing.Title}' ilanÃƒâ€Ã‚Â±nÃƒâ€Ã‚Â±z {newStatus.ToDisplayText()} durumuna alÃƒâ€Ã‚Â±ndÃƒâ€Ã‚Â±. Detay: {safeNote}";

        await _notificationService.CreateNotificationAsync(
            owner.Id,
            title,
            message,
            NotificationType.SystemNotification,
            listing.Id,
            "Listing");

        var emailBody = EmailTemplateBuilder.BuildActionEmailTemplate(
            title,
            "Ãƒâ€Ã‚Â°lan Moderasyon GÃƒÆ’Ã‚Â¼ncellemesi",
            owner.FullName ?? owner.UserName ?? "KullanÃƒâ€Ã‚Â±cÃƒâ€Ã‚Â±",
            message,
            detailLink,
            "Ãƒâ€Ã‚Â°lan DetayÃƒâ€Ã‚Â±nÃƒâ€Ã‚Â± AÃƒÆ’Ã‚Â§");

        await _emailService.SendEmailAsync(owner.Email, $"ArtaniPaylas - {title}", emailBody);
    }

    private static string? NormalizeText(string? input)
    {
        return string.IsNullOrWhiteSpace(input) ? null : input.Trim();
    }

    private static string SimplifyBoundaryGeoJson(string rawBoundary)
    {
        try
        {
            var feature = JsonSerializer.Deserialize<BoundaryFeature>(rawBoundary);
            if (feature?.Geometry?.Coordinates is null)
            {
                return "{}";
            }

            foreach (var polygon in feature.Geometry.Coordinates)
            {
                if (polygon is null)
                {
                    continue;
                }

                for (var ringIndex = 0; ringIndex < polygon.Count; ringIndex++)
                {
                    var ring = polygon[ringIndex];
                    if (ring is null || ring.Count <= 180)
                    {
                        continue;
                    }

                    var step = Math.Max(1, (int)Math.Ceiling(ring.Count / 180d));
                    var reduced = new List<List<double>>();

                    for (var pointIndex = 0; pointIndex < ring.Count; pointIndex += step)
                    {
                        reduced.Add(ring[pointIndex]);
                    }

                    var lastPoint = ring[^1];
                    var lastReduced = reduced[^1];
                    if (lastReduced[0] != lastPoint[0] || lastReduced[1] != lastPoint[1])
                    {
                        reduced.Add(lastPoint);
                    }

                    polygon[ringIndex] = reduced;
                }
            }

            return JsonSerializer.Serialize(feature);
        }
        catch
        {
            return "{}";
        }
    }

    private sealed class ActivityItem
    {
        public DateTime CreatedAt { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    private sealed class MapPointDto
    {
        public int Index { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private sealed class BoundaryFeature
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("properties")]
        public Dictionary<string, object?> Properties { get; set; } = new();

        [JsonPropertyName("geometry")]
        public BoundaryGeometry? Geometry { get; set; }
    }

    private sealed class BoundaryGeometry
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("coordinates")]
        public List<List<List<List<double>>>>? Coordinates { get; set; }
    }
}
