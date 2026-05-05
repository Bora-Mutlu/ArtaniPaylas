namespace ArtaniPaylas.Core.Enums;

/// <summary>
/// Bildirim tipleri
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Yeni talep geldi
    /// </summary>
    RequestReceived = 1,

    /// <summary>
    /// Talep onaylandı
    /// </summary>
    RequestApproved = 2,

    /// <summary>
    /// Talep reddedildi
    /// </summary>
    RequestRejected = 3,

    /// <summary>
    /// Ürün teslim edildi
    /// </summary>
    ItemDelivered = 4,

    /// <summary>
    /// Yorum alındı
    /// </summary>
    ReviewReceived = 5,

    /// <summary>
    /// Ürün yakında sona erecek
    /// </summary>
    ListingExpiringSoon = 6,

    /// <summary>
    /// E-posta doğrulama gerekli
    /// </summary>
    EmailVerificationRequired = 7,

    /// <summary>
    /// Sistem bildirimi
    /// </summary>
    SystemNotification = 8,

    /// <summary>
    /// Yeni ilan yayımlandı
    /// </summary>
    NewListingPublished = 9
}

