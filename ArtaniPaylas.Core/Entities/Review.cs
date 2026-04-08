using System.ComponentModel.DataAnnotations;

namespace ArtaniPaylas.Core.Entities;

public class Review
{
    public int Id { get; set; }

    public int RequestId { get; set; }

    [Required]
    public string FromUserId { get; set; } = string.Empty;

    [Required]
    public string ToUserId { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Request? Request { get; set; }

    public ApplicationUser? FromUser { get; set; }

    public ApplicationUser? ToUser { get; set; }
}
