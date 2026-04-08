using System.ComponentModel.DataAnnotations;

namespace ArtaniPaylas.Core.ViewModels;

public class ProfileEditViewModel
{
    [Required(ErrorMessage = "Ad soyad zorunludur.")]
    [StringLength(50, ErrorMessage = "Ad soyad en fazla 50 karakter olabilir.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefon numarası zorunludur.")]
    [RegularExpression(@"^\+?[0-9\s\-\(\)]{10,20}$", ErrorMessage = "Geçerli bir telefon numarası gir.")]
    [StringLength(20, ErrorMessage = "Telefon numarası en fazla 20 karakter olabilir.")]
    [Display(Name = "Telefon Numarası")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yaş zorunludur.")]
    [Range(13, 99, ErrorMessage = "Yaş 13 ile 99 arasında olmalıdır.")]
    public int Age { get; set; }

    [Required(ErrorMessage = "Konum zorunludur.")]
    [StringLength(100, ErrorMessage = "Konum en fazla 100 karakter olabilir.")]
    public string Location { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Profil Fotoğrafı URL")]
    public string? ProfileImageUrl { get; set; }
}
