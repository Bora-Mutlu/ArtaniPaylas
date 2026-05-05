using ArtaniPaylas.Core.Entities;
using ArtaniPaylas.Core.Enums;
using ArtaniPaylas.Data;
using ArtaniPaylas.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        var controller = new AdminController(context, null!, null!, null!);
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

        var controller = new AdminController(context, null!, null!, null!);
        var result = await controller.UpdateReportStatus(report.Id, ReportStatus.Reviewed);

        Assert.IsType<RedirectToActionResult>(result);
        var reloaded = await context.Reports.FindAsync(report.Id);
        Assert.Equal(ReportStatus.Reviewed, reloaded!.Status);
    }

    private static ApplicationDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
