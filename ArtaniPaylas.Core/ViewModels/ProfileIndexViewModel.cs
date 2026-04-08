using ArtaniPaylas.Core.Entities;

namespace ArtaniPaylas.Core.ViewModels;

public class ProfileIndexViewModel
{
    public ApplicationUser User { get; set; } = default!;

    public IReadOnlyCollection<Listing> MyListings { get; set; } = Array.Empty<Listing>();

    public IReadOnlyCollection<Request> IncomingRequests { get; set; } = Array.Empty<Request>();

    public IReadOnlyCollection<Request> OutgoingRequests { get; set; } = Array.Empty<Request>();
}
