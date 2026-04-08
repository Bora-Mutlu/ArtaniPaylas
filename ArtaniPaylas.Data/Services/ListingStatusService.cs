using ArtaniPaylas.Core.Enums;
using ArtaniPaylas.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ArtaniPaylas.Data.Services;

public class ListingStatusService : IListingStatusService
{
    private readonly ApplicationDbContext _context;

    public ListingStatusService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task UpdateExpiredListingsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        var expiredListings = await _context.Listings
            .Where(x => x.Status == ListingStatus.Active && x.ExpirationDate.Date < today)
            .ToListAsync(cancellationToken);

        if (expiredListings.Count == 0)
        {
            return;
        }

        foreach (var listing in expiredListings)
        {
            listing.Status = ListingStatus.Expired;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
