namespace ArtaniPaylas.Core.Interfaces;

/// <summary>
/// Güvenilir kullanıcı servisi interface'i
/// </summary>
public interface ITrustedUserService
{
    /// <summary>
    /// Kullanıcının güvenilir kullanıcı olup olmadığını kontrol eder
    /// Kriteler: Email doğrulanmış + min 4.0 rating + min 1 işlem
    /// </summary>
    /// <param name="userId">Kullanıcı ID</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    Task<bool> IsTrustedUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcının güven skorunu hesaplar ve güvenilir status'unu günceller
    /// </summary>
    /// <param name="userId">Kullanıcı ID</param>
    /// <param name="cancellationToken">İptal tokeni</param>
    Task<bool> CalculateAndUpdateTrustStatusAsync(string userId, CancellationToken cancellationToken = default);
}

