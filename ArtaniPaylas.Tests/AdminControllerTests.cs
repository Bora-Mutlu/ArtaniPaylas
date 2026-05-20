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

public class AdminControllerTests
{
    [Fact]
    public async Task UpdateReportStatus_ShouldReject_InvalidTransition()
    {
        await using var context = BuildContext();
        var report = new Report
        {
            ReporterId = "reporter-1",
            Reason = "Spam",
            Status = ReportStatus.Resolved
        };
        context.Reports.Add(report);
        await context.SaveChangesAsync();

        var controller = BuildController(context);
        var result = await controller.UpdateReportStatus(report.Id, ReportStatus.Reviewed);

        Assert.IsType<RedirectToActionResult>(result);
        var reloaded = await context.Reports.FindAsync(report.Id);
        Assert.Equal(ReportStatus.Resolved, reloaded!.Status);
    }

    [Fact]
    public async Task UpdateReportStatus_ShouldAllow_PendingToReviewed()
    {
        await using var context = BuildContext();
        var report = new Report
        {
            ReporterId = "reporter-1",
            Reason = "Uygunsuz icerik",
            Status = ReportStatus.Pending
        };
        context.Reports.Add(report);
        await context.SaveChangesAsync();

        var controller = BuildController(context);
        var result = await controller.UpdateReportStatus(report.Id, ReportStatus.Reviewed);

        Assert.IsType<RedirectToActionResult>(result);
        var reloaded = await context.Reports.FindAsync(report.Id);
        Assert.Equal(ReportStatus.Reviewed, reloaded!.Status);
    }

    [Fact]
    public async Task UpdateAppointment_ShouldAllow_PendingToApproved_WithFutureDate()
    {
        await using var context = BuildContext();
        var request = await AddRequestAsync(context, RequestStatus.Pending);
        var futureDate = DateTime.Now.AddDays(1);

        var controller = BuildController(context);
        var result = await controller.UpdateAppointment(request.Id, RequestStatus.Approved, futureDate, "Uygun.");

        Assert.IsType<RedirectToActionResult>(result);
        var reloaded = await context.Requests.FindAsync(request.Id);
        Assert.Equal(RequestStatus.Approved, reloaded!.Status);
        Assert.NotNull(reloaded.ConfirmedAppointmentAt);
        Assert.Equal("Uygun.", reloaded.MunicipalityNote);
    }

    [Fact]
    public async Task UpdateAppointment_ShouldReject_InvalidTransition()
    {
        await using var context = BuildContext();
        var request = await AddRequestAsync(context, RequestStatus.Pending);

        var controller = BuildController(context);
        var result = await controller.UpdateAppointment(request.Id, RequestStatus.Delivered, null, null);

        Assert.IsType<RedirectToActionResult>(result);
        var reloaded = await context.Requests.FindAsync(request.Id);
        Assert.Equal(RequestStatus.Pending, reloaded!.Status);
        Assert.Null(reloaded.ConfirmedAppointmentAt);
    }

    [Fact]
    public async Task UpdateAppointment_ShouldReject_PastApprovalDate()
    {
        await using var context = BuildContext();
        var request = await AddRequestAsync(context, RequestStatus.Pending);

        var controller = BuildController(context);
        var result = await controller.UpdateAppointment(request.Id, RequestStatus.Approved, DateTime.Now.AddHours(-1), null);

        Assert.IsType<RedirectToActionResult>(result);
        var reloaded = await context.Requests.FindAsync(request.Id);
        Assert.Equal(RequestStatus.Pending, reloaded!.Status);
        Assert.Null(reloaded.ConfirmedAppointmentAt);
    }

    [Fact]
    public async Task UpdateAppointment_ShouldReject_DeliveredWithoutConfirmedAppointment()
    {
        await using var context = BuildContext();
        var request = await AddRequestAsync(context, RequestStatus.Approved);

        var controller = BuildController(context);
        var result = await controller.UpdateAppointment(request.Id, RequestStatus.Delivered, null, null);

        Assert.IsType<RedirectToActionResult>(result);
        var reloaded = await context.Requests.FindAsync(request.Id);
        Assert.Equal(RequestStatus.Approved, reloaded!.Status);
    }

    [Fact]
    public async Task UpdateAppointment_ShouldCompleteListing_WhenDelivered()
    {
        await using var context = BuildContext();
        var confirmedAt = DateTime.UtcNow.AddDays(1);
        var request = await AddRequestAsync(context, RequestStatus.Approved, confirmedAt);

        var controller = BuildController(context);
        var result = await controller.UpdateAppointment(request.Id, RequestStatus.Delivered, null, "Teslim edildi.");

        Assert.IsType<RedirectToActionResult>(result);
        var reloaded = await context.Requests
            .Include(x => x.Listing)
            .FirstAsync(x => x.Id == request.Id);
        Assert.Equal(RequestStatus.Delivered, reloaded.Status);
        Assert.Equal(ListingStatus.Completed, reloaded.Listing!.Status);
    }

    private static ApplicationDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static AdminController BuildController(ApplicationDbContext context)
    {
        var controller = new AdminController(context, null!, new FakeNotificationService(), new FakeEmailService(), null!);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        return controller;
    }

    private static async Task<Request> AddRequestAsync(
        ApplicationDbContext context,
        RequestStatus status,
        DateTime? confirmedAppointmentAt = null)
    {
        var owner = new ApplicationUser
        {
            Id = $"owner-{Guid.NewGuid()}",
            UserName = "owner@example.com",
            Email = "owner@example.com"
        };
        var requester = new ApplicationUser
        {
            Id = $"requester-{Guid.NewGuid()}",
            UserName = "requester@example.com",
            Email = "requester@example.com"
        };
        var listing = new Listing
        {
            OwnerUserId = owner.Id,
            OwnerUser = owner,
            Title = "Mont",
            Description = "Temiz mont",
            Location = "Ayancik",
            Status = ListingStatus.Active,
            ExpirationDate = DateTime.UtcNow.AddDays(10)
        };
        var request = new Request
        {
            Listing = listing,
            RequesterUser = requester,
            RequesterUserId = requester.Id,
            RequestedAppointmentAt = DateTime.UtcNow.AddDays(1),
            ConfirmedAppointmentAt = confirmedAppointmentAt,
            Status = status
        };

        context.Users.AddRange(owner, requester);
        context.Listings.Add(listing);
        context.Requests.Add(request);
        await context.SaveChangesAsync();

        return request;
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

    private sealed class FakeNotificationService : INotificationService
    {
        public Task CreateNotificationAsync(
            string userId,
            string title,
            string message,
            NotificationType type,
            int? relatedEntityId = null,
            string? relatedEntityType = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IEnumerable<dynamic>> GetUserNotificationsAsync(string userId, int limit = 20, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<dynamic>>(Array.Empty<object>());
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

    private sealed class FakeEmailService : IEmailService
    {
        public Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
