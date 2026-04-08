using ArtaniPaylas.Core.Enums;

namespace ArtaniPaylas.Core.Extensions;

public static class StatusLocalizationExtensions
{
    public static string ToDisplayText(this ListingStatus status)
    {
        return status switch
        {
            ListingStatus.Active => "Aktif",
            ListingStatus.Completed => "Tamamlandı",
            ListingStatus.Expired => "Süresi Geçti",
            ListingStatus.Canceled => "İptal Edildi",
            _ => status.ToString()
        };
    }

    public static string ToDisplayText(this RequestStatus status)
    {
        return status switch
        {
            RequestStatus.Pending => "Bekliyor",
            RequestStatus.Approved => "Onaylandı",
            RequestStatus.Rejected => "Reddedildi",
            RequestStatus.Delivered => "Teslim Edildi",
            _ => status.ToString()
        };
    }
}
