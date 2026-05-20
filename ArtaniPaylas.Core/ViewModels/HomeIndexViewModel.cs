using ArtaniPaylas.Core.Entities;

namespace ArtaniPaylas.Core.ViewModels;

public class HomeIndexViewModel
{
    public IReadOnlyCollection<ContainerLocation> Containers { get; set; } = Array.Empty<ContainerLocation>();

    public IReadOnlyCollection<Listing> FeaturedListings { get; set; } = Array.Empty<Listing>();

    public int ActiveListingCount { get; set; }

    public int ContainerCount { get; set; }
}
