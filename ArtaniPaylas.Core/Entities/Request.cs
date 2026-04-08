using System.ComponentModel.DataAnnotations;
using ArtaniPaylas.Core.Enums;

namespace ArtaniPaylas.Core.Entities;

public class Request
{
    public int Id { get; set; }

    public int ListingId { get; set; }

    [Required]
    public string RequesterUserId { get; set; } = string.Empty;

    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Listing? Listing { get; set; }

    public ApplicationUser? RequesterUser { get; set; }
}
