using PropSeguro.Domain.Users;

namespace PropSeguro.Api.Email;

public interface IBetaInvitationEmailService
{
    Task SendInvitationAsync(AppUser user, string temporaryPassword, string loginUrl, CancellationToken cancellationToken);
}
