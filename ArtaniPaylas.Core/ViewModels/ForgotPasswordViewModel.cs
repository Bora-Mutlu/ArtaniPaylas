using System.ComponentModel.DataAnnotations;

namespace ArtaniPaylas.Core.ViewModels;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "E-posta adresi zorunludur.")]
    [EmailAddress(ErrorMessage = "Lütfen geçerli bir e-posta adresi girin.")]
    public string Email { get; set; } = string.Empty;
}

