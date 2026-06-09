namespace PropSeguro.Application.Common;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    bool IsAdmin { get; }
}
