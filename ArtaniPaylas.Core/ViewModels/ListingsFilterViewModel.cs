namespace ArtaniPaylas.Core.ViewModels;

public class ListingsFilterViewModel
{
    public string? SearchTitle { get; set; }

    public string? SearchLocation { get; set; }

    public bool ActiveOnly { get; set; } = true;
}
