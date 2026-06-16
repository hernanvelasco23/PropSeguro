namespace PropSeguro.Api.Email;

public sealed class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Email sending is disabled. Provider=Null To={ToEmail} Subject={Subject} HtmlLength={HtmlLength} TextLength={TextLength}",
            message.ToEmail,
            message.Subject,
            message.HtmlBody.Length,
            message.TextBody?.Length ?? 0);

        return Task.CompletedTask;
    }
}
