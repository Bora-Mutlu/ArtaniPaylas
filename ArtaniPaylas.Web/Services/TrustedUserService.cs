using ArtaniPaylas.Core.Interfaces;
using ArtaniPaylas.Data;
using Microsoft.EntityFrameworkCore;

namespace ArtaniPaylas.Web.Services;

/// <summary>
/// Güvenilir kullanıcı servisi - Trust score ve badge yönetimi
/// </summary>
public class TrustedUserService : ITrustedUserService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TrustedUserService> _logger;

    // Güvenilir kullanıcı kriterleri
    private const double MIN_TRUST_SCORE = 4.0;
    private const int CACHE_DURATION_MINUTES = 60;

    public TrustedUserService(
        ApplicationDbContext context,
        ILogger<TrustedUserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> IsTrustedUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
            {
                return false;
            }

            // Cache kontrol et (1 saat geçerli)
            if (user.TrustedBadgeCalculatedAt.HasValue && 
                DateTime.UtcNow.Subtract(user.TrustedBadgeCalculatedAt.Value).TotalMinutes < CACHE_DURATION_MINUTES)
            {
                return user.IsTrusted;
            }

            // Fresh calculation
            return await CalculateAndUpdateTrustStatusAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking trusted status for user {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> CalculateAndUpdateTrustStatusAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
            {
                return false;
            }

            // Güvenilir kullanıcı kriterleri kontrol et
            bool isTrusted = await CheckTrustCriteriaAsync(userId, cancellationToken);

            // Database'i güncelle
            user.IsTrusted = isTrusted;
            user.TrustedBadgeCalculatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Trust status updated for user {UserId}: {IsTrusted}", userId, isTrusted);

            return isTrusted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating trust status for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Güvenilir kullanıcı kriterlerini kontrol et
    /// Kriterler (ALL required):
    /// 1. E-posta doğrulanmış (EmailConfirmedAt != null)
    /// 2. Minimum 4.0 trust score
    /// 3. En az 1 işlem (Request completed veya Review received)
    /// </summary>
    private async Task<bool> CheckTrustCriteriaAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        // Criterion 1: Email doğrulanmış mı?
        if (!user.EmailConfirmedAt.HasValue)
        {
            return false;
        }

        // Criterion 2: Minimum trust score
        if (user.TrustScore < MIN_TRUST_SCORE)
        {
            return false;
        }

        // Criterion 3: Min işlem var mı?
        // Tamamlanan request veya alınan review var mı?
        var completedRequests = await _context.Requests
            .Where(r => r.RequesterUserId == userId && r.Status == Core.Enums.RequestStatus.Delivered)
            .CountAsync(cancellationToken);

        var receivedReviews = await _context.Reviews
            .Where(r => r.ToUserId == userId)
            .CountAsync(cancellationToken);

        if (completedRequests < 1 && receivedReviews < 1)
        {
            return false;
        }

        return true;
    }
}

