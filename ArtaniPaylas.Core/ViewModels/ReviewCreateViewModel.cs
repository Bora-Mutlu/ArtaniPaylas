using System.ComponentModel.DataAnnotations;

namespace ArtaniPaylas.Core.ViewModels;

public class ReviewCreateViewModel
{
    public int RequestId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; } = 5;

    [StringLength(1000)]
    public string? Comment { get; set; }

    public string TargetUserDisplayName { get; set; } = string.Empty;
}
