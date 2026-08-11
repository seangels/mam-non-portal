using AdminPortal.Domain.Enums;

namespace AdminPortal.Application.Common.Models;

public sealed record ActorContext(Guid UserId, Guid SessionId, UserRole Role, string? IpAddress);
