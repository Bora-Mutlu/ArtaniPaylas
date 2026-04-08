using ArtaniPaylas.Core.Entities;

namespace ArtaniPaylas.Core.ViewModels;

public class ListingsIndexViewModel
{
    public ListingsFilterViewModel Filter { get; set; } = new();

    public IReadOnlyCollection<Listing> Listings { get; set; } = Array.Empty<Listing>();
}
