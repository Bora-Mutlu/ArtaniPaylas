namespace ArtaniPaylas.Core.ViewModels;

public class ListingsFilterViewModel
{
    public string? SearchTitle { get; set; }

    public string? SearchLocation { get; set; }

    public string? Category { get; set; }

    public string? Size { get; set; }

    public string? Gender { get; set; }

    public string? AgeGroup { get; set; }

    public string? District { get; set; }

    public bool ActiveOnly { get; set; } = true;
}
