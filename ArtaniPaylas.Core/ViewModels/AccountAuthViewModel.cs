namespace ArtaniPaylas.Core.ViewModels;

public class AccountAuthViewModel
{
    public LoginViewModel Login { get; set; } = new();

    public RegisterViewModel Register { get; set; } = new();

    public string ActivePanel { get; set; } = "login";

    public string? ReturnUrl { get; set; }
}
