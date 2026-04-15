using System.ComponentModel.DataAnnotations;
using ArtaniPaylas.Core.Enums;

namespace ArtaniPaylas.Core.Entities;

public class Report
{
    public int Id { get; set; }

    [Required]
    public string ReporterId { get; set; } = string.Empty;

    public ApplicationUser? Reporter { get; set; }

    // Id of the reported User or Listing, based on the target type
    public string? ReportedUserId { get; set; }
    public ApplicationUser? ReportedUser { get; set; }

    public int? ReportedListingId { get; set; }
    public Listing? ReportedListing { get; set; }

    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    public ReportStatus Status { get; set; } = ReportStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
