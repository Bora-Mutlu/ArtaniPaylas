using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ArtaniPaylas.Core.Entities;

public class ApplicationUser : IdentityUser
{
    [StringLength(50)]
    public string? FullName { get; set; }

    [Range(13, 99)]
    public int? Age { get; set; }

    [StringLength(200)]
    public string? Location { get; set; }

    [StringLength(260)]
    public string? ProfileImageUrl { get; set; }

    public double TrustScore { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// E-posta doğrulama tarihi (null = doğrulanmamış)
    /// </summary>
    public DateTime? EmailConfirmedAt { get; set; }

    /// <summary>
    /// E-posta doğrulama tokeni (hash'lenmiş)
    /// </summary>
    [StringLength(500)]
    public string? ConfirmationToken { get; set; }

    /// <summary>
    /// Doğrulama tokeni expire tarihi
    /// </summary>
    public DateTime? ConfirmationTokenExpiresAt { get; set; }

    /// <summary>
    /// Güvenilir kullanıcı durumu (cache için)
    /// </summary>
    public bool IsTrusted { get; set; } = false;

    /// <summary>
    /// Yeni ilan yayınlandığında e-posta almak ister mi
    /// </summary>
    public bool NotifyOnNewListingsByEmail { get; set; } = false;

    /// <summary>
    /// Güvenilir status'un son hesaplandığı tarih
    /// </summary>
    public DateTime? TrustedBadgeCalculatedAt { get; set; }

    public ICollection<Listing> Listings { get; set; } = new List<Listing>();

    public ICollection<Request> RequestsMade { get; set; } = new List<Request>();

    public ICollection<Review> ReviewsGiven { get; set; } = new List<Review>();

    public ICollection<Review> ReviewsReceived { get; set; } = new List<Review>();
}

