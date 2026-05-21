using ArtaniPaylas.Core.Entities;

namespace ArtaniPaylas.Core.ViewModels;

public class ProfileHistoryViewModel
{
    public IReadOnlyCollection<Request> DeliveredOutgoingRequests { get; set; } = Array.Empty<Request>();
}
