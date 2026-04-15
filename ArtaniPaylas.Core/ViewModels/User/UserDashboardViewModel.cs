using System;
using System.Collections.Generic;

namespace ArtaniPaylas.Core.ViewModels;

public class UserDashboardViewModel
{
    public string DisplayName { get; set; } = "Kullanıcı";
    public string? ProfileImageUrl { get; set; }

    public int ActiveListingsCount { get; set; }
    public int NewListingsLast7Days { get; set; }
    public int IncomingRequestsCount { get; set; }
    public int PendingIncomingRequestsCount { get; set; }
    public double OutgoingSuccessRate { get; set; }

    public IReadOnlyCollection<UserDashboardListingItemViewModel> RecentListings { get; set; }
        = Array.Empty<UserDashboardListingItemViewModel>();

    public IReadOnlyCollection<UserDashboardPendingRequestItemViewModel> PendingIncomingRequests { get; set; }
        = Array.Empty<UserDashboardPendingRequestItemViewModel>();
}

public class UserDashboardListingItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? PhotoPath { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public bool IsEditable { get; set; }
    public int RequestCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserDashboardPendingRequestItemViewModel
{
    public int RequestId { get; set; }
    public string RequesterInitials { get; set; } = "AP";
    public string RequesterName { get; set; } = "Kullanıcı";
    public string ListingTitle { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
