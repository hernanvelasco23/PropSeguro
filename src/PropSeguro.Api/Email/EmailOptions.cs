namespace PropSeguro.Api.Email;

public sealed class EmailOptions
{
    public string Provider { get; set; } = "Null";

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "PropSegura";

    public SmtpEmailOptions Smtp { get; set; } = new();
}

public sealed class SmtpEmailOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool EnableSsl { get; set; } = true;
}
