using System.ComponentModel.DataAnnotations;

namespace ArtaniPaylas.Core.Entities;

public class ListingPhoto
{
    public int Id { get; set; }

    public int ListingId { get; set; }

    [Required]
    [StringLength(260)]
    public string PhotoPath { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Listing? Listing { get; set; }
}
