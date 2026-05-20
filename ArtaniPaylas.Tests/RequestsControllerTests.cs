using System.Security.Claims;
using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.Enums;
using ArtaniPaylas.Core.Interfaces;
using ArtaniPaylas.Data;
using ArtaniPaylas.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ArtaniPaylas.Tests;

public class RequestsControllerTests
{
    [Fact]
    public async Task MarkDelivered_ShouldReject_WhenCurrentStatusIsPending()
    {
        await using var context = BuildContext();
        SeedUsers(context);
        var listing = SeedListing(context, "owner-1");
        var request = new Request
        {
            ListingId = listing.Id,
            RequesterUserId = "requester-1",
            RequestedAppointmentAt = DateTime.UtcNow.AddDays(1),
            Status = RequestStatus.Pending
        };
        context.Requests.Add(request);
        await context.SaveChangesAsync();

        var controller = BuildController(context, "owner-1");
        var result = await controller.MarkDelivered(request.Id);

        Assert.IsType<RedirectToActionResult>(result);
        var reloaded = await context.Requests.FindAsync(request.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(RequestStatus.Pending, reloaded!.Status);
    }

    [Fact]
    public async Task Approve_ShouldConfirmAppointment_WhenCurrentStatusIsPending()
    {
        await using var context = BuildContext();
        SeedUsers(context);
        var listing = SeedListing(context, "owner-1");
        var approvedTarget = new Request
        {
            ListingId = listing.Id,
            RequesterUserId = "requester-1",
            RequestedAppointmentAt = DateTime.UtcNow.AddDays(2),
            Status = RequestStatus.Pending
        };
        var anotherPending = new Request
        {
            ListingId = listing.Id,
            RequesterUserId = "requester-2",
            RequestedAppointmentAt = DateTime.UtcNow.AddDays(3),
            Status = RequestStatus.Pending
        };
        context.Requests.AddRange(approvedTarget, anotherPending);
        await context.SaveChangesAsync();

        var controller = BuildController(context, "owner-1");
        var result = await controller.Approve(approvedTarget.Id);

        Assert.IsType<RedirectToActionResult>(result);
        var updatedTarget = await context.Requests.FindAsync(approvedTarget.Id);
        var updatedOther = await context.Requests.FindAsync(anotherPending.Id);
        var updatedListing = await context.Listings.FindAsync(listing.Id);

        Assert.Equal(RequestStatus.Approved, updatedTarget!.Status);
        Assert.NotNull(updatedTarget.ConfirmedAppointmentAt);
        Assert.Equal(RequestStatus.Pending, updatedOther!.Status);
        Assert.Equal(ListingStatus.Active, updatedListing!.Status);
    }

    private static RequestsController BuildController(ApplicationDbContext context, string userId)
    {
        var controller = new RequestsController(context, new NoopNotificationService());
        var httpContext = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        httpContext.User = new ClaimsPrincipal(identity);
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        return controller;
    }

    private static ApplicationDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedUsers(ApplicationDbContext context)
    {
        context.Users.AddRange(
            new ApplicationUser { Id = "owner-1", UserName = "owner@test.local", Email = "owner@test.local", EmailConfirmedAt = DateTime.UtcNow },
            new ApplicationUser { Id = "requester-1", UserName = "r1@test.local", Email = "r1@test.local", EmailConfirmedAt = DateTime.UtcNow },
            new ApplicationUser { Id = "requester-2", UserName = "r2@test.local", Email = "r2@test.local", EmailConfirmedAt = DateTime.UtcNow });
        context.SaveChanges();
    }

    private static Listing SeedListing(ApplicationDbContext context, string ownerId)
    {
        var listing = new Listing
        {
            OwnerUserId = ownerId,
            Title = "Masaustu Lamba",
            Description = "Temiz urun",
            Location = "Istanbul",
            ExpirationDate = DateTime.UtcNow.AddDays(10),
            Status = ListingStatus.Active
        };
        context.Listings.Add(listing);
        context.SaveChanges();
        return listing;
    }

    private sealed class NoopNotificationService : INotificationService
    {
        public Task CreateNotificationAsync(string userId, string title, string message, NotificationType type, int? relatedEntityId = null, string? relatedEntityType = null, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IEnumerable<dynamic>> GetUserNotificationsAsync(string userId, int limit = 20, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<dynamic>>(Array.Empty<dynamic>());
        }

        public Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteNotificationAsync(int notificationId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
