using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ArtaniPaylas.Core.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Ad Soyad alanı zorunludur.")]
    [StringLength(50, ErrorMessage = "Ad Soyad en fazla 50 karakter olabilir.")]
    [Display(Name = "Ad Soyad")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta alanı zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [StringLength(120, ErrorMessage = "E-posta en fazla 120 karakter olabilir.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefon numarası alanı zorunludur.")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "Telefon numarası 11 haneli olmalıdır (05xx...).")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yaş alanı zorunludur.")]
    [Range(13, 99, ErrorMessage = "Yaşınız 13 ile 99 arasında olmalıdır.")]
    public int? Age { get; set; }

    [Required(ErrorMessage = "Konum alanı zorunludur.")]
    [StringLength(100, ErrorMessage = "Konum alanı en fazla 100 karakter olabilir.")]
    public string Location { get; set; } = string.Empty;

    public IFormFile? ProfileImageFile { get; set; }

    [Required(ErrorMessage = "Şifre alanı zorunludur.")]
    [DataType(DataType.Password)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{6,}$", 
        ErrorMessage = "Şifre en az 6 karakter, 1 büyük harf, 1 küçük harf ve 1 sembol içermelidir.")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Girdiğiniz şifreler eşleşmiyor.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
