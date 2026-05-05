using ArtaniPaylas.Core.Enums;

namespace ArtaniPaylas.Core.Interfaces;

/// <summary>
/// Bildirim servisi interface'i
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Yeni bildirim oluşturur
    /// </summary>
    /// <param name="userId">Bildirim alan kullanıcı ID</param>
    /// <param name="title">Bildirim başlığı</param>
    /// <param name="message">Bildirim mesajı</param>
    /// <param name="type">Bildirim tipi</param>
    /// <param name="relatedEntityId">İlgili entity ID (opsiyonel)</param>
    /// <param name="relatedEntityType">İlgili entity tipi (opsiyonel)</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    Task CreateNotificationAsync(
        string userId,
        string title,
        string message,
        NotificationType type,
        int? relatedEntityId = null,
        string? relatedEntityType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcının tüm bildirimlerini alır (son N adet)
    /// </summary>
    /// <param name="userId">Kullanıcı ID</param>
    /// <param name="limit">Kaç adet almak istiyoruz</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    Task<IEnumerable<dynamic>> GetUserNotificationsAsync(string userId, int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Okunmamış bildirim sayısını alır
    /// </summary>
    /// <param name="userId">Kullanıcı ID</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bildirimi okundu olarak işaretler
    /// </summary>
    /// <param name="notificationId">Bildirim ID</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    Task MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bildirimi siler
    /// </summary>
    /// <param name="notificationId">Bildirim ID</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    Task DeleteNotificationAsync(int notificationId, CancellationToken cancellationToken = default);
}

