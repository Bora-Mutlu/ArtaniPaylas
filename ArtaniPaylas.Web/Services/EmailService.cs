using ArtaniPaylas.Core.Interfaces;
using System.Net;
using System.Net.Mail;

namespace ArtaniPaylas.Web.Services;

/// <summary>
/// SMTP üzerinden email gönderimi servisi
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        try
        {
            var smtpServer = _configuration["Email:SmtpServer"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var senderEmail = _configuration["Email:SenderEmail"];
            var senderName = _configuration["Email:SenderName"];
            var username = _configuration["Email:Username"];
            var password = _configuration["Email:Password"];

            if (string.IsNullOrWhiteSpace(smtpServer) || string.IsNullOrWhiteSpace(senderEmail))
            {
                const string message = "Email configuration incomplete. Please check Email:SmtpServer and Email:SenderEmail settings.";
                _logger.LogError(message + " Recipient: {ToEmail}", toEmail);
                throw new InvalidOperationException(message);
            }

            using (var client = new SmtpClient(smtpServer, smtpPort))
            {
                client.EnableSsl = true;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;

                if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
                {
                    client.Credentials = new NetworkCredential(username, password);
                }

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };

                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage, cancellationToken);

                _logger.LogInformation("Email successfully sent to {ToEmail} with subject: {Subject}", toEmail, subject);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {ToEmail}", toEmail);
            throw;
        }
    }
}

