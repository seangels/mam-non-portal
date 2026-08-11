using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Enums;

namespace AdminPortal.UnitTests;

public sealed class AuthorizationRulesTests
{
    [Theory]
    [InlineData(UserRole.SuperAdmin, UserRole.Admin)]
    [InlineData(UserRole.SuperAdmin, UserRole.Teacher)]
    [InlineData(UserRole.Admin, UserRole.Teacher)]
    public void EnsureCanManageUserAllowsExpectedPairs(UserRole actorRole, UserRole targetRole)
    {
        var actor = new ActorContext(Guid.NewGuid(), Guid.NewGuid(), actorRole, null);

        AuthorizationRules.EnsureCanManageUser(actor, targetRole);
    }

    [Theory]
    [InlineData(UserRole.Admin, UserRole.Admin)]
    [InlineData(UserRole.Admin, UserRole.SuperAdmin)]
    [InlineData(UserRole.SuperAdmin, UserRole.SuperAdmin)]
    [InlineData(UserRole.Teacher, UserRole.Teacher)]
    public void EnsureCanManageUserRejectsForbiddenPairs(UserRole actorRole, UserRole targetRole)
    {
        var actor = new ActorContext(Guid.NewGuid(), Guid.NewGuid(), actorRole, null);

        Assert.Throws<ForbiddenException>(() => AuthorizationRules.EnsureCanManageUser(actor, targetRole));
    }
}
