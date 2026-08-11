using System.Text.Json;
using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Setup;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using AdminPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AdminPortal.Infrastructure.Setup;

public sealed class SetupService(
    AdminPortalDbContext dbContext,
    IPasswordService passwordService,
    TimeProvider timeProvider) : ISetupService
{
    // Serializes setup requests across API instances using the same PostgreSQL database.
    private const long SetupAdvisoryLockKey = 4_361_293_843_522;

    public async Task<SetupStatusResponse> GetStatusAsync(CancellationToken cancellationToken)
    {
        var hasUsers = await dbContext.Users.IgnoreQueryFilters().AnyAsync(cancellationToken);
        return new SetupStatusResponse(!hasUsers);
    }

    public async Task<SetupSuperAdminResponse> CreateSuperAdminAsync(
        CreateSuperAdminRequest request,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        EnsureRequired(request.Email, "email", "Email là bắt buộc.");
        EnsureRequired(request.FullName, "fullName", "Họ tên là bắt buộc.");
        PasswordPolicy.Validate(request.Password);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            $"SELECT pg_advisory_xact_lock({SetupAdvisoryLockKey})",
            cancellationToken);

        if (await dbContext.Users.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            throw new ConflictException("Hệ thống đã được khởi tạo.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim(),
            NormalizedEmail = EmailNormalizer.Normalize(request.Email),
            PasswordHash = string.Empty,
            FullName = request.FullName.Trim(),
            Role = UserRole.SuperAdmin,
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordService.Hash(user, request.Password);

        dbContext.Users.Add(user);
        dbContext.AuditLogs.Add(new AuditLog
        {
            Action = "System.SuperAdminInitialized",
            EntityType = "User",
            EntityId = user.Id,
            NewValues = JsonSerializer.Serialize(new
            {
                user.Email,
                user.FullName,
                Role = user.Role.ToString(),
                Status = user.Status.ToString()
            }),
            IpAddress = ipAddress,
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SetupSuperAdminResponse(user.Id, user.Email, user.FullName);
    }

    private static void EnsureRequired(string value, string field, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AppValidationException(message, new Dictionary<string, string[]>
            {
                [field] = [message]
            });
        }
    }
}
