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

    public ICollection<Listing> Listings { get; set; } = new List<Listing>();

    public ICollection<Request> RequestsMade { get; set; } = new List<Request>();

    public ICollection<Review> ReviewsGiven { get; set; } = new List<Review>();

    public ICollection<Review> ReviewsReceived { get; set; } = new List<Review>();
}
