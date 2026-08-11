using System.IdentityModel.Tokens.Jwt;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Common.Models;
using AdminPortal.Domain.Enums;

namespace AdminPortal.Api.Authentication;

public sealed class HttpCurrentActor(IHttpContextAccessor httpContextAccessor) : ICurrentActor
{
    public ActorContext GetRequired()
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new UnauthorizedException("Không có HTTP context.");
        var principal = context.User;

        if (!Guid.TryParse(principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var userId) ||
            !Guid.TryParse(principal.FindFirst("sid")?.Value, out var sessionId) ||
            !Enum.TryParse<UserRole>(principal.FindFirst("role")?.Value, true, out var role))
        {
            throw new UnauthorizedException("Access token không hợp lệ.");
        }

        return new ActorContext(userId, sessionId, role, context.Connection.RemoteIpAddress?.ToString());
    }
}
