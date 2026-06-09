namespace PropSeguro.Domain.BetaAccess;

public sealed class BetaAccessRequest
{
    private BetaAccessRequest()
    {
        FullName = string.Empty;
        Email = string.Empty;
        Status = BetaAccessRequestStatus.Pending;
    }

    public BetaAccessRequest(string fullName, string email, string? phone, string? intendedUse, string? message)
    {
        Id = Guid.NewGuid();
        FullName = string.IsNullOrWhiteSpace(fullName) ? throw new ArgumentException("Full name is required.", nameof(fullName)) : fullName.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? throw new ArgumentException("Email is required.", nameof(email)) : email.Trim().ToLowerInvariant();
        Phone = NormalizeOptionalText(phone);
        IntendedUse = NormalizeOptionalText(intendedUse);
        Message = NormalizeOptionalText(message);
        Status = BetaAccessRequestStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string FullName { get; private set; }
    public string Email { get; private set; }
    public string? Phone { get; private set; }
    public string? IntendedUse { get; private set; }
    public string? Message { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public BetaAccessRequestStatus Status { get; private set; }

    public void Approve()
    {
        Status = BetaAccessRequestStatus.Approved;
    }

    public void Reject()
    {
        Status = BetaAccessRequestStatus.Rejected;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public enum BetaAccessRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}
