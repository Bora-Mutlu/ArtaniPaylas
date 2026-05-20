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

[Authorize]
public class RequestsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;

    public RequestsController(ApplicationDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppointmentCreateViewModel model)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || !user.EmailConfirmedAt.HasValue)
        {
            TempData["ErrorMessage"] = "Randevu oluşturmak için lütfen e-posta adresinizi doğrulayın.";
            return RedirectToAction("Details", "Listings", new { id = model.ListingId });
        }

        if (model.RequestedAppointmentAt <= DateTime.Now)
        {
            TempData["ErrorMessage"] = "Lütfen ileri bir tarih ve saat seçin.";
            return RedirectToAction("Details", "Listings", new { id = model.ListingId });
        }

        var listing = await _context.Listings
            .Include(x => x.Requests)
            .FirstOrDefaultAsync(x => x.Id == model.ListingId);

        if (listing is null)
        {
            return NotFound();
        }

        if (listing.OwnerUserId == userId)
        {
            TempData["ErrorMessage"] = "Kendi ilanın için randevu oluşturamazsın.";
            return RedirectToAction("Details", "Listings", new { id = model.ListingId });
        }

        if (listing.Status != ListingStatus.Active)
        {
            TempData["ErrorMessage"] = "Bu ilan için randevu alımı kapalı.";
            return RedirectToAction("Details", "Listings", new { id = model.ListingId });
        }

        var hasOpenRequest = listing.Requests.Any(x =>
            x.RequesterUserId == userId &&
            x.Status != RequestStatus.Rejected &&
            x.Status != RequestStatus.Delivered);

        if (hasOpenRequest)
        {
            TempData["ErrorMessage"] = "Bu ilan için zaten açık bir randevu kaydın var.";
            return RedirectToAction("Details", "Listings", new { id = model.ListingId });
        }

        var request = new Request
        {
            ListingId = listing.Id,
            RequesterUserId = userId,
            RequestedAppointmentAt = DateTime.SpecifyKind(model.RequestedAppointmentAt, DateTimeKind.Local).ToUniversalTime(),
            RequesterNote = NormalizeText(model.RequesterNote),
            Status = RequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Requests.Add(request);
        await _context.SaveChangesAsync();

        var requesterName = user.FullName ?? "Bir kullanıcı";
        await _notificationService.CreateNotificationAsync(
            listing.OwnerUserId,
            "İlanınız İçin Yeni Randevu Talebi",
            $"'{listing.Title}' ilanı için {requesterName} randevu oluşturdu.",
            NotificationType.RequestReceived,
            request.Id,
            "Request");

        TempData["SuccessMessage"] = "Randevu talebin belediye onayına gönderildi.";
        return RedirectToAction(nameof(Outgoing));
    }

    [Authorize(Roles = "Admin")]
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

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        return await UpdateRequestStatusAsync(id, RequestStatus.Approved);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        return await UpdateRequestStatusAsync(id, RequestStatus.Rejected);
    }

    [Authorize(Roles = "Admin")]
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

        if (!IsValidRequestStatusTransition(request.Status, targetStatus))
        {
            TempData["ErrorMessage"] = "Bu randevu için geçersiz durum geçişi.";
            return RedirectToAction(nameof(Incoming));
        }

        request.Status = targetStatus;
        request.UpdatedAt = DateTime.UtcNow;
        request.ConfirmedAppointmentAt ??= request.RequestedAppointmentAt;

        await _context.SaveChangesAsync();
        await TriggerNotificationAsync(request, targetStatus);

        TempData["SuccessMessage"] = "Randevu durumu güncellendi.";
        return RedirectToAction(nameof(Incoming));
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private static bool IsValidRequestStatusTransition(RequestStatus currentStatus, RequestStatus targetStatus)
    {
        return currentStatus switch
        {
            RequestStatus.Pending => targetStatus is RequestStatus.Approved or RequestStatus.Rejected,
            RequestStatus.Approved => targetStatus == RequestStatus.Delivered,
            _ => false
        };
    }

    private async Task TriggerNotificationAsync(Request request, RequestStatus targetStatus)
    {
        try
        {
            switch (targetStatus)
            {
                case RequestStatus.Approved:
                    await _notificationService.CreateNotificationAsync(
                        request.RequesterUserId,
                        "Randevun Onaylandı",
                        "Belediye hesabı randevu talebini onayladı.",
                        NotificationType.RequestApproved,
                        request.Id,
                        "Request");
                    break;

                case RequestStatus.Rejected:
                    await _notificationService.CreateNotificationAsync(
                        request.RequesterUserId,
                        "Randevu Talebin Reddedildi",
                        "Seçtiğin saat aralığı uygun bulunmadı.",
                        NotificationType.RequestRejected,
                        request.Id,
                        "Request");
                    break;

                case RequestStatus.Delivered:
                    await _notificationService.CreateNotificationAsync(
                        request.RequesterUserId,
                        "Randevu Süreci Tamamlandı",
                        "Randevu kaydın tamamlandı olarak işaretlendi.",
                        NotificationType.ItemDelivered,
                        request.Id,
                        "Request");
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Notification error: {ex.Message}");
        }
    }

    private static string? NormalizeText(string? input)
    {
        return string.IsNullOrWhiteSpace(input) ? null : input.Trim();
    }
}
