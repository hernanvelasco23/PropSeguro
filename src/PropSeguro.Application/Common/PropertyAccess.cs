using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Application.Common;

public static class PropertyAccess
{
    public static bool CanAccess(this ICurrentUserContext? currentUser, PropertyAnalysis analysis)
    {
        if (currentUser?.UserId is null)
        {
            return true;
        }

        return currentUser.IsAdmin || analysis.UserId == currentUser.UserId.Value;
    }
}
