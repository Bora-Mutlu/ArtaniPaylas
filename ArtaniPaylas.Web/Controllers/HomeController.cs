using System.Diagnostics;
using ArtaniPaylas.Core.Enums;
using ArtaniPaylas.Core.ViewModels;
using ArtaniPaylas.Data;
using ArtaniPaylas.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace ArtaniPaylas.Web.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var containers = await _context.ContainerLocations
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.District)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var listings = await _context.Listings
            .AsNoTracking()
            .Include(x => x.Photos.OrderBy(p => p.SortOrder))
            .Where(x => x.Status == ListingStatus.Active)
            .OrderByDescending(x => x.CreatedAt)
            .Take(4)
            .ToListAsync();

        var model = new HomeIndexViewModel
        {
            Containers = containers,
            FeaturedListings = listings,
            ActiveListingCount = await _context.Listings.CountAsync(x => x.Status == ListingStatus.Active),
            ContainerCount = containers.Count
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
