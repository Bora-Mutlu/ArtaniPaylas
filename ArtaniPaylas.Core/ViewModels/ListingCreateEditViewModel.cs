using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ArtaniPaylas.Core.ViewModels;

public class ListingCreateEditViewModel
{
    public int? Id { get; set; }

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

    [Required]
    [DataType(DataType.Date)]
    public DateTime ExpirationDate { get; set; } = DateTime.Today.AddDays(1);

    [Required]
    [StringLength(200)]
    public string Location { get; set; } = string.Empty;

    [StringLength(120)]
    public string? District { get; set; }

    [StringLength(500)]
    public string? ContactNote { get; set; }

    public List<IFormFile> Photos { get; set; } = new();

    public List<string> ExistingPhotoPaths { get; set; } = new();

    public List<ExistingListingPhotoViewModel> ExistingPhotos { get; set; } = new();

    public List<int> DeletedPhotoIds { get; set; } = new();
}

public class ExistingListingPhotoViewModel
{
    public int Id { get; set; }

    public string PhotoPath { get; set; } = string.Empty;
}
