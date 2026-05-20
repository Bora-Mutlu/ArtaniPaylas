using System.ComponentModel.DataAnnotations;
using ArtaniPaylas.Core.Enums;

namespace ArtaniPaylas.Core.Entities;

public class Listing
{
    public int Id { get; set; }

    [Required]
    public string OwnerUserId { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string Category { get; set; } = "Genel Giyim";

    [StringLength(40)]
    public string? Size { get; set; }

    [StringLength(40)]
    public string? Gender { get; set; }

    [StringLength(40)]
    public string? AgeGroup { get; set; }

    [StringLength(80)]
    public string? Condition { get; set; }

    [StringLength(260)]
    public string? PhotoPath { get; set; }

    [DataType(DataType.Date)]
    public DateTime ExpirationDate { get; set; }

    [Required]
    [StringLength(200)]
    public string Location { get; set; } = string.Empty;

    [StringLength(120)]
    public string? District { get; set; }

    [StringLength(500)]
    public string? ContactNote { get; set; }

    public ListingStatus Status { get; set; } = ListingStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? OwnerUser { get; set; }

    public ICollection<ListingPhoto> Photos { get; set; } = new List<ListingPhoto>();

    public ICollection<Request> Requests { get; set; } = new List<Request>();
}
