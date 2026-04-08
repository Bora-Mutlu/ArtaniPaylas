namespace ArtaniPaylas.Core.Interfaces;

public interface IListingStatusService
{
    Task UpdateExpiredListingsAsync(CancellationToken cancellationToken = default);
}
