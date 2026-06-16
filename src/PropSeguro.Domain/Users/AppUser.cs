namespace PropSeguro.Domain.Users;

public sealed class AppUser
{
    private AppUser()
    {
        Email = string.Empty;
        FullName = string.Empty;
        PasswordHash = string.Empty;
        Role = UserRole.User;
        Status = UserStatus.Invited;
    }

    public AppUser(string email, string fullName, string passwordHash, UserRole role, UserStatus status = UserStatus.Active)
    {
        Id = Guid.NewGuid();
        Email = NormalizeEmail(email);
        FullName = string.IsNullOrWhiteSpace(fullName) ? throw new ArgumentException("Full name is required.", nameof(fullName)) : fullName.Trim();
        PasswordHash = string.IsNullOrWhiteSpace(passwordHash) ? throw new ArgumentException("Password hash is required.", nameof(passwordHash)) : passwordHash;
        Role = role;
        Status = status;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string FullName { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }

    public void MarkLogin()
    {
        LastLoginAtUtc = DateTime.UtcNow;
    }

    public void Enable()
    {
        Status = UserStatus.Active;
    }

    public void MakeAdmin()
    {
        Role = UserRole.Admin;
    }

    public void UpdateProfile(string fullName)
    {
        FullName = string.IsNullOrWhiteSpace(fullName) ? throw new ArgumentException("Full name is required.", nameof(fullName)) : fullName.Trim();
    }

    public void UpdatePasswordHash(string passwordHash)
    {
        PasswordHash = string.IsNullOrWhiteSpace(passwordHash) ? throw new ArgumentException("Password hash is required.", nameof(passwordHash)) : passwordHash;
    }

    public void Disable()
    {
        Status = UserStatus.Disabled;
    }

    public static string NormalizeEmail(string email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? throw new ArgumentException("Email is required.", nameof(email))
            : email.Trim().ToLowerInvariant();
    }
}

public enum UserRole
{
    Admin = 1,
    User = 2
}

public enum UserStatus
{
    Invited = 1,
    Active = 2,
    Disabled = 3
}
