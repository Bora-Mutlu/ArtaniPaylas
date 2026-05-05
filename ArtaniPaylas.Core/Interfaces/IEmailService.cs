namespace ArtaniPaylas.Core.Interfaces;

/// <summary>
/// Email gönderimi için servis interface'i
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// E-posta gönderir
    /// </summary>
    /// <param name="toEmail">Alıcı e-posta adresi</param>
    /// <param name="subject">E-postanın konusu</param>
    /// <param name="body">E-postanın içeriği</param>
    /// <param name="isHtml">HTML formatında mı?</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    /// <returns></returns>
    Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default);
}

