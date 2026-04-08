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

    [StringLength(260)]
    public string? PhotoPath { get; set; }

    [DataType(DataType.Date)]
    public DateTime ExpirationDate { get; set; }

    [Required]
    [StringLength(200)]
    public string Location { get; set; } = string.Empty;

    public ListingStatus Status { get; set; } = ListingStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? OwnerUser { get; set; }

    public ICollection<Request> Requests { get; set; } = new List<Request>();
}
