using ArtaniPaylas.Core.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ArtaniPaylas.Core.Entities;

/// <summary>
/// Kullanıcı bildirim entity'si
/// </summary>
public class Notification
{
    /// <summary>
    /// Bildirim ID'si
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Bildirim alan kullanıcı ID'si
    /// </summary>
    [Required]
    public string UserId { get; set; } = null!;

    /// <summary>
    /// Kullanıcı referansı
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// Bildirim başlığı
    /// </summary>
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = null!;

    /// <summary>
    /// Bildirim mesajı
    /// </summary>
    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = null!;

    /// <summary>
    /// Bildirim tipi
    /// </summary>
    [Required]
    public NotificationType Type { get; set; }

    /// <summary>
    /// İlgili entity ID'si (Request, Review vb.)
    /// </summary>
    public int? RelatedEntityId { get; set; }

    /// <summary>
    /// İlgili entity tipi (Request, Review vb.)
    /// </summary>
    [StringLength(50)]
    public string? RelatedEntityType { get; set; }

    /// <summary>
    /// Okundu mu?
    /// </summary>
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// Oluşturulma tarihi
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Okunma tarihi (null ise okunmamış)
    /// </summary>
    public DateTime? ReadAt { get; set; }
}

