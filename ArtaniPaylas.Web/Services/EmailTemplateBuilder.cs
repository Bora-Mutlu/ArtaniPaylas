namespace ArtaniPaylas.Web.Services;

public static class EmailTemplateBuilder
{
    public static string BuildEmailConfirmationTemplate(string fullName, string confirmationLink, bool isResend)
    {
        var title = isResend ? "E-posta doğrulama bağlantısı" : "Hesabını doğrula";
        var subtitle = isResend
            ? "Talebin üzerine doğrulama bağlantını tekrar oluşturduk."
            : "Aramıza hoş geldin. Hesabını etkinleştirmek için son bir adım kaldı.";

        return $@"
<!doctype html>
<html lang=""tr"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
  <title>{title}</title>
</head>
<body style=""margin:0;padding:0;background:#f4f6fb;font-family:'Segoe UI',Arial,sans-serif;color:#17212f;"">
  <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""padding:28px 12px;background:#f4f6fb;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""max-width:620px;background:#ffffff;border-radius:14px;overflow:hidden;border:1px solid #dde5f3;"">
          <tr>
            <td style=""padding:20px 24px 10px;"">
              <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"">
                <tr>
                  <td style=""font-size:13px;font-weight:700;color:#31507f;letter-spacing:.4px;"">ArtaniPaylas</td>
                  <td align=""right"">
                    <span style=""display:inline-block;background:#eaf0ff;color:#21427a;padding:5px 10px;border-radius:999px;font-size:11px;font-weight:700;"">Doğrulama</span>
                  </td>
                </tr>
              </table>
              <h1 style=""margin:12px 0 6px;font-size:30px;line-height:1.18;color:#0e1e3a;"">{title}</h1>
              <p style=""margin:0;font-size:15px;line-height:1.6;color:#53627a;"">{subtitle}</p>
            </td>
          </tr>
          <tr><td style=""padding:0 24px;""><div style=""height:1px;background:#ebf0f8;""></div></td></tr>
          <tr><td style=""padding:18px 24px 8px;""><p style=""margin:0 0 6px;font-size:18px;color:#0f1f39;"">Merhaba <strong>{System.Net.WebUtility.HtmlEncode(fullName)}</strong>,</p></td></tr>
          <tr>
            <td style=""padding:0 24px 22px;"">
              <p style=""margin:0 0 18px;font-size:15px;line-height:1.7;color:#42536e;"">
                Hesabını güvenle kullanabilmek için aşağıdaki butondan e-posta adresini doğrula.
              </p>
              <p style=""margin:0 0 24px;"">
                <a href=""{confirmationLink}"" style=""display:inline-block;background:#1357c2;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:10px;font-weight:700;font-size:14px;"">
                  E-postayı doğrula
                </a>
              </p>
              <p style=""margin:0 0 8px;font-size:12px;color:#73819a;"">Buton çalışmazsa bu bağlantıyı kullan:</p>
              <p style=""margin:0 0 16px;font-size:12px;line-height:1.6;word-break:break-all;color:#31507f;"">{confirmationLink}</p>
              <p style=""margin:0;font-size:12px;color:#7d8aa1;"">Bu bağlantı 24 saat geçerlidir.</p>
            </td>
          </tr>
          <tr><td style=""height:1px;background:#ebf0f8;""></td></tr>
          <tr><td style=""padding:14px 24px;background:#f8faff;""><p style=""margin:0;font-size:12px;color:#7d8aa1;"">Bu e-posta ArtaniPaylas sistemi tarafından otomatik gönderilmiştir.</p></td></tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    public static string BuildActionEmailTemplate(
        string title,
        string subtitle,
        string recipientName,
        string message,
        string? actionUrl = null,
        string? actionLabel = null)
    {
        var safeTitle = System.Net.WebUtility.HtmlEncode(title);
        var safeSubtitle = System.Net.WebUtility.HtmlEncode(subtitle);
        var safeRecipient = System.Net.WebUtility.HtmlEncode(recipientName);
        var safeMessage = System.Net.WebUtility.HtmlEncode(message);
        var safeActionUrl = System.Net.WebUtility.HtmlEncode(actionUrl ?? string.Empty);
        var safeActionLabel = System.Net.WebUtility.HtmlEncode(actionLabel ?? "Detayı Gör");

        var actionBlock = string.IsNullOrWhiteSpace(actionUrl)
            ? string.Empty
            : $@"
              <p style=""margin:0 0 20px;"">
                <a href=""{safeActionUrl}"" style=""display:inline-block;background:linear-gradient(135deg,#006948,#008f62);color:#ffffff;text-decoration:none;padding:12px 22px;border-radius:10px;font-weight:700;font-size:14px;"">
                  {safeActionLabel}
                </a>
              </p>
              <p style=""margin:0 0 16px;font-size:12px;line-height:1.5;color:#5b6b66;word-break:break-all;"">{safeActionUrl}</p>";

        return $@"
<!doctype html>
<html lang=""tr"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width,initial-scale=1"" />
  <title>{safeTitle}</title>
</head>
<body style=""margin:0;padding:0;background:#f4f6fb;font-family:'Segoe UI',Arial,sans-serif;color:#17212f;"">
  <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""padding:28px 12px;background:#f4f6fb;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""max-width:640px;background:#ffffff;border-radius:14px;overflow:hidden;border:1px solid #dde5f3;"">
          <tr>
            <td style=""padding:20px 24px 10px;"">
              <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"">
                <tr>
                  <td style=""font-size:13px;font-weight:700;color:#31507f;letter-spacing:.4px;"">ArtaniPaylas</td>
                  <td align=""right"">
                    <span style=""display:inline-block;background:#eaf0ff;color:#21427a;padding:5px 10px;border-radius:999px;font-size:11px;font-weight:700;"">Bildirim</span>
                  </td>
                </tr>
              </table>
              <h1 style=""margin:12px 0 6px;font-size:30px;line-height:1.18;color:#0e1e3a;"">{safeTitle}</h1>
              <p style=""margin:0;font-size:15px;line-height:1.6;color:#53627a;"">{safeSubtitle}</p>
            </td>
          </tr>
          <tr><td style=""padding:0 24px;""><div style=""height:1px;background:#ebf0f8;""></div></td></tr>
          <tr>
            <td style=""padding:18px 24px 22px;"">
              <p style=""margin:0 0 8px;font-size:18px;color:#0f1f39;"">Merhaba <strong>{safeRecipient}</strong>,</p>
              <p style=""margin:0 0 18px;font-size:15px;line-height:1.7;color:#42536e;"">{safeMessage}</p>
              {actionBlock}
            </td>
          </tr>
          <tr><td style=""height:1px;background:#ebf0f8;""></td></tr>
          <tr><td style=""padding:14px 24px;background:#f8faff;""><p style=""margin:0;font-size:12px;color:#7d8aa1;"">Bu e-posta ArtaniPaylas tarafından otomatik gönderilmiştir.</p></td></tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";
    }
}

