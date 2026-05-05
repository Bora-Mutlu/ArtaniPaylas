using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.Enums;
using ArtaniPaylas.Core.Interfaces;
using ArtaniPaylas.Data;
using Microsoft.EntityFrameworkCore;

namespace ArtaniPaylas.Web.Services;

/// <summary>
/// Bildirim servisi - veritabanında bildirim yönetimi ve e-posta tetikleme
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext context,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task CreateNotificationAsync(
        string userId,
        string title,
        string message,
        NotificationType type,
        int? relatedEntityId = null,
        string? relatedEntityType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                RelatedEntityId = relatedEntityId,
                RelatedEntityType = relatedEntityType,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync(cancellationToken);

            await TriggerEmailNotificationAsync(userId, title, message, type, relatedEntityId, relatedEntityType, cancellationToken);
            _logger.LogInformation("Notification created for user {UserId}: {Title}", userId, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification for user {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<dynamic>> GetUserNotificationsAsync(string userId, int limit = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(limit)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.Type,
                    n.IsRead,
                    n.CreatedAt,
                    n.RelatedEntityId,
                    n.RelatedEntityType
                })
                .ToListAsync(cancellationToken);

            return notifications;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notifications for user {UserId}", userId);
            throw;
        }
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unread count for user {UserId}", userId);
            throw;
        }
    }

    public async Task MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = await _context.Notifications.FindAsync(new object[] { notificationId }, cancellationToken: cancellationToken);

            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Notification {NotificationId} marked as read", notificationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read", notificationId);
            throw;
        }
    }

    public async Task DeleteNotificationAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = await _context.Notifications.FindAsync(new object[] { notificationId }, cancellationToken: cancellationToken);

            if (notification != null)
            {
                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Notification {NotificationId} deleted", notificationId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {NotificationId}", notificationId);
            throw;
        }
    }

    private async Task TriggerEmailNotificationAsync(
        string userId,
        string title,
        string message,
        NotificationType type,
        int? relatedEntityId = null,
        string? relatedEntityType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var criticalTypes = new[]
            {
                NotificationType.RequestReceived,
                NotificationType.RequestApproved,
                NotificationType.RequestRejected,
                NotificationType.ItemDelivered,
                NotificationType.ReviewReceived,
                NotificationType.NewListingPublished
            };

            if (!criticalTypes.Contains(type))
            {
                return;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            var (actionUrl, actionLabel) = await BuildActionLinkAsync(type, relatedEntityId, relatedEntityType, cancellationToken);
            var emailBody = EmailTemplateBuilder.BuildActionEmailTemplate(
                title,
                "Bildirim Merkezi",
                user.FullName ?? user.UserName ?? "Kullanıcı",
                message,
                actionUrl,
                actionLabel);

            await _emailService.SendEmailAsync(user.Email, $"ArtaniPaylas: {title}", emailBody, true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering email notification for user {UserId}", userId);
        }
    }

    private async Task<(string? Url, string Label)> BuildActionLinkAsync(
        NotificationType type,
        int? relatedEntityId,
        string? relatedEntityType,
        CancellationToken cancellationToken)
    {
        var baseUrl = (_configuration["App:BaseUrl"] ?? "http://localhost:5067").TrimEnd('/');

        if (type == NotificationType.RequestReceived)
        {
            return ($"{baseUrl}/Requests/Incoming", "Gelen Talepleri Gör");
        }

        if (type == NotificationType.RequestApproved || type == NotificationType.RequestRejected || type == NotificationType.ItemDelivered)
        {
            if (relatedEntityType == "Request" && relatedEntityId.HasValue)
            {
                var request = await _context.Requests
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == relatedEntityId.Value, cancellationToken);

                if (request != null)
                {
                    return ($"{baseUrl}/Listings/Details/{request.ListingId}", "İlan Detayına Git");
                }
            }

            return ($"{baseUrl}/Requests/Outgoing", "Talep Durumunu Gör");
        }

        if (type == NotificationType.ReviewReceived)
        {
            return ($"{baseUrl}/Profile/History", "Profil Geçmişini Gör");
        }

        if (type == NotificationType.NewListingPublished)
        {
            if (relatedEntityType == "Listing" && relatedEntityId.HasValue)
            {
                return ($"{baseUrl}/Listings/Details/{relatedEntityId.Value}", "Yeni İlanı Gör");
            }

            return ($"{baseUrl}/Listings", "İlanları Gör");
        }

        return (null, "Detayı Gör");
    }
}
