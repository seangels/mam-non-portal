using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.Common;

public static class AuthorizationRules
{
    public static void EnsurePortalManager(ActorContext actor)
    {
        if (actor.Role is not (UserRole.SuperAdmin or UserRole.Admin))
        {
            throw new ForbiddenException("Tài khoản không có quyền quản trị.");
        }
    }

    public static void EnsureCanManageUser(ActorContext actor, UserRole targetRole)
    {
        EnsurePortalManager(actor);
        var allowed = actor.Role switch
        {
            UserRole.SuperAdmin => targetRole is UserRole.Admin or UserRole.Teacher,
            UserRole.Admin => targetRole == UserRole.Teacher,
            _ => false
        };

        if (!allowed)
        {
            throw new ForbiddenException("Bạn không có quyền quản lý tài khoản này.");
        }
    }
}
