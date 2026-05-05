using System.Security.Claims;
using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.Enums;
using ArtaniPaylas.Core.Interfaces;
using ArtaniPaylas.Data;
using ArtaniPaylas.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    public async Task Approve_ShouldCompleteListing_AndRejectOtherPendingRequests()
    {
        await using var context = BuildContext();
        SeedUsers(context);
        var listing = SeedListing(context, "owner-1");
        var approvedTarget = new Request { ListingId = listing.Id, RequesterUserId = "requester-1", Status = RequestStatus.Pending };
        var anotherPending = new Request { ListingId = listing.Id, RequesterUserId = "requester-2", Status = RequestStatus.Pending };
        context.Requests.AddRange(approvedTarget, anotherPending);
        await context.SaveChangesAsync();

        var controller = BuildController(context, "owner-1");
        var result = await controller.Approve(approvedTarget.Id);

        Assert.IsType<RedirectToActionResult>(result);
        var updatedTarget = await context.Requests.FindAsync(approvedTarget.Id);
        var updatedOther = await context.Requests.FindAsync(anotherPending.Id);
        var updatedListing = await context.Listings.FindAsync(listing.Id);

        Assert.Equal(RequestStatus.Approved, updatedTarget!.Status);
        Assert.Equal(RequestStatus.Rejected, updatedOther!.Status);
        Assert.Equal(ListingStatus.Completed, updatedListing!.Status);
    }

    private static RequestsController BuildController(ApplicationDbContext context, string userId)
    {
        var controller = new RequestsController(context, new NoopNotificationService());
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
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
        public Task CreateNotificationAsync(string userId, string title, string message, NotificationType type, int? relatedEntityId = null, string? relatedEntityType = null)
        {
            return Task.CompletedTask;
        }

        public Task<List<Notification>> GetUserNotificationsAsync(string userId, bool unreadOnly = false, int skip = 0, int take = 20)
        {
            return Task.FromResult(new List<Notification>());
        }

        public Task<int> GetUnreadCountAsync(string userId)
        {
            return Task.FromResult(0);
        }

        public Task<bool> MarkAsReadAsync(int notificationId, string userId)
        {
            return Task.FromResult(true);
        }

        public Task<int> MarkAllAsReadAsync(string userId)
        {
            return Task.FromResult(0);
        }
    }
}
