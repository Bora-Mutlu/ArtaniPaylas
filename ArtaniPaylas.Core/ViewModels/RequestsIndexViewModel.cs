using ArtaniPaylas.Core.Entities;

namespace ArtaniPaylas.Core.ViewModels;

public class RequestsIndexViewModel
{
    public IReadOnlyCollection<Request> Requests { get; set; } = Array.Empty<Request>();
}
