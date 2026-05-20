using System.ComponentModel.DataAnnotations;

namespace ArtaniPaylas.Core.Entities;

public class ContainerLocation
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Address { get; set; }

    [StringLength(120)]
    public string? District { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
