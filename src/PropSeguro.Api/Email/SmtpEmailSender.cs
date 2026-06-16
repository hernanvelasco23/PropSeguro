using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace PropSeguro.Api.Email;

public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(this.options.FromName, this.options.FromEmail));
        mimeMessage.To.Add(new MailboxAddress(message.ToName ?? message.ToEmail, message.ToEmail));
        mimeMessage.Subject = message.Subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        };
        mimeMessage.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = ResolveSocketOptions(this.options.Smtp);

        await client.ConnectAsync(this.options.Smtp.Host, this.options.Smtp.Port, socketOptions, cancellationToken);
        await client.AuthenticateAsync(this.options.Smtp.Username, this.options.Smtp.Password, cancellationToken);
        await client.SendAsync(mimeMessage, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation("Invitation email sent through SMTP to {ToEmail}.", message.ToEmail);
    }

    public static SecureSocketOptions ResolveSocketOptions(SmtpEmailOptions options)
    {
        if (!options.EnableSsl)
        {
            return SecureSocketOptions.Auto;
        }

        return options.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
    }
}
