using System.Security.Cryptography;
using System.Text;

namespace ArtaniPaylas.Core.Helpers;

/// <summary>
/// Token oluşturma yardımcı sınıfı
/// </summary>
public static class TokenHelper
{
    /// <summary>
    /// Rastgele token oluşturur (e-posta doğrulama vb. için)
    /// </summary>
    /// <param name="length">Token uzunluğu (default: 32)</param>
    /// <returns>URL-safe token string</returns>
    public static string GenerateRandomToken(int length = 32)
    {
        byte[] tokenData = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenData);
        }
        return Convert.ToBase64String(tokenData)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "")
            .Substring(0, Math.Min(length, length));
    }

    /// <summary>
    /// Token hash'leyerek saklamak için (veritabanında depolamak daha güvenli)
    /// </summary>
    /// <param name="token">Hash'lenecek token</param>
    /// <returns>SHA256 hash</returns>
    public static string HashToken(string token)
    {
        using (var sha256 = SHA256.Create())
        {
            byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}

