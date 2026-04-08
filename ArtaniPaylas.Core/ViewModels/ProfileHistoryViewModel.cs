using ArtaniPaylas.Core.Entities;

namespace ArtaniPaylas.Core.ViewModels;

public class ProfileHistoryViewModel
{
    public IReadOnlyCollection<Request> DeliveredIncomingRequests { get; set; } = Array.Empty<Request>();

    public IReadOnlyCollection<Request> DeliveredOutgoingRequests { get; set; } = Array.Empty<Request>();

    public IReadOnlyCollection<Review> ReceivedReviews { get; set; } = Array.Empty<Review>();

    public IReadOnlyCollection<Review> GivenReviews { get; set; } = Array.Empty<Review>();
}
