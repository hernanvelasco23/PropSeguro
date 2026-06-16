namespace PropSeguro.Api.Email;

public sealed record EmailMessage(
    string ToEmail,
    string? ToName,
    string Subject,
    string HtmlBody,
    string? TextBody);
