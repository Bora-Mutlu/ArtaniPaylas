using System.ComponentModel.DataAnnotations;

namespace ArtaniPaylas.Core.ViewModels;

public class AppointmentCreateViewModel
{
    public int ListingId { get; set; }

    [Required]
    [DataType(DataType.DateTime)]
    public DateTime RequestedAppointmentAt { get; set; } = DateTime.Now.AddDays(1);

    [StringLength(500)]
    public string? RequesterNote { get; set; }
}
