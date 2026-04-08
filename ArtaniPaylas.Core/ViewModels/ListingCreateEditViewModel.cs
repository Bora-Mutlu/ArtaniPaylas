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
    [DataType(DataType.Date)]
    public DateTime ExpirationDate { get; set; } = DateTime.Today.AddDays(1);

    [Required]
    [StringLength(200)]
    public string Location { get; set; } = string.Empty;

    public IFormFile? Photo { get; set; }

    public string? ExistingPhotoPath { get; set; }
}
