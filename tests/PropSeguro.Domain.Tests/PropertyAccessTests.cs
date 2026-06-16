using PropSeguro.Application.Common;
using PropSeguro.Domain.PropertyAnalyses;

namespace PropSeguro.Domain.Tests;

public sealed class PropertyAccessTests
{
    [Fact]
    public void CanAccess_returns_false_when_current_user_is_null()
    {
        var analysis = CreateAnalysis(Guid.NewGuid());
        ICurrentUserContext? currentUser = null;

        var canAccess = currentUser.CanAccess(analysis);

        Assert.False(canAccess);
    }

    [Fact]
    public void CanAccess_returns_true_for_admin()
    {
        var analysis = CreateAnalysis(Guid.NewGuid());
        var currentUser = new TestCurrentUserContext(Guid.NewGuid(), isAdmin: true);

        var canAccess = currentUser.CanAccess(analysis);

        Assert.True(canAccess);
    }

    [Fact]
    public void CanAccess_returns_true_for_owner_and_false_for_other_user()
    {
        var ownerId = Guid.NewGuid();
        var analysis = CreateAnalysis(ownerId);

        Assert.True(new TestCurrentUserContext(ownerId, isAdmin: false).CanAccess(analysis));
        Assert.False(new TestCurrentUserContext(Guid.NewGuid(), isAdmin: false).CanAccess(analysis));
    }

    private static PropertyAnalysis CreateAnalysis(Guid ownerId)
    {
        var analysis = new PropertyAnalysis(
            "Departamento",
            "https://example.com",
            "Av. Santa Fe 1234",
            "Palermo",
            80000,
            100000,
            40,
            36,
            2,
            1,
            1,
            10,
            3,
            null,
            null,
            true,
            true,
            true,
            PropertyCondition.Good,
            null);
        analysis.AssignOwner(ownerId);
        return analysis;
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public TestCurrentUserContext(Guid? userId, bool isAdmin)
        {
            UserId = userId;
            IsAdmin = isAdmin;
        }

        public Guid? UserId { get; }

        public string? Email => "test@propsegura.local";

        public string? Role => IsAdmin ? "Admin" : "User";

        public bool IsAdmin { get; }
    }
}
