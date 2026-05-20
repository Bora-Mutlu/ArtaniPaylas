using ArtaniPaylas.Core.Entities;

namespace ArtaniPaylas.Core.ViewModels;

public class AdminContainersViewModel
{
    public IReadOnlyCollection<ContainerLocation> Containers { get; set; } = Array.Empty<ContainerLocation>();

    public string BoundaryGeoJson { get; set; } = "{}";
}
