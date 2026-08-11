using AdminPortal.Application.Common;
using AdminPortal.Application.Common.Exceptions;
using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Domain.Entities;
using AdminPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AdminPortal.Application.Users;

public sealed class UserAccountCoordinator(
    IApplicationDbContext dbContext,
    IPasswordService passwordService)
{
    public async Task<User> CreateAsync(
        string email,
        string fullName,
        string? phoneNumber,
        UserRole role,
        UserStatus status,
        string password,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        PasswordPolicy.Validate(password);
        var normalizedEmail = await ValidateAsync(email, fullName, null, cancellationToken);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = string.Empty,
            FullName = fullName.Trim(),
            PhoneNumber = NormalizeOptional(phoneNumber),
            Role = role,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };
        user.PasswordHash = passwordService.Hash(user, password);
        dbContext.Users.Add(user);
        return user;
    }

    public async Task<bool> UpdateAsync(
        User user,
        string email,
        string fullName,
        string? phoneNumber,
        UserStatus status,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = await ValidateAsync(email, fullName, user.Id, cancellationToken);
        var fullNameValue = fullName.Trim();
        var nameChanged = !string.Equals(user.FullName, fullNameValue, StringComparison.Ordinal);
        var statusChanged = user.Status != status;

        user.Email = email.Trim();
        user.NormalizedEmail = normalizedEmail;
        user.FullName = fullNameValue;
        user.PhoneNumber = NormalizeOptional(phoneNumber);
        user.Status = status;
        user.UpdatedAt = now;

        if (statusChanged)
        {
            await RevokeSessionsAsync(user.Id, now, cancellationToken);
        }

        return nameChanged;
    }

    public async Task ChangePasswordAsync(
        User user,
        string password,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        PasswordPolicy.Validate(password);
        user.PasswordHash = passwordService.Hash(user, password);
        user.UpdatedAt = now;
        await RevokeSessionsAsync(user.Id, now, cancellationToken);
    }

    public async Task SoftDeleteAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        user.DeletedAt = now;
        user.UpdatedAt = now;
        await RevokeSessionsAsync(user.Id, now, cancellationToken);
    }

    private async Task<string> ValidateAsync(
        string email,
        string fullName,
        Guid? excludedUserId,
        CancellationToken cancellationToken)
    {
        EnsureText(email, "email", "Email không được để trống.");
        EnsureText(fullName, "fullName", "Họ tên không được để trống.");
        var normalizedEmail = EmailNormalizer.Normalize(email);
        if (await dbContext.Users.AnyAsync(
                user => user.NormalizedEmail == normalizedEmail &&
                    (excludedUserId == null || user.Id != excludedUserId),
                cancellationToken))
        {
            throw new ConflictException("Email đã được sử dụng.", ProblemCodes.EmailAlreadyExists);
        }

        return normalizedEmail;
    }

    private Task<int> RevokeSessionsAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.AuthSessions
            .Where(session => session.UserId == userId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAt, now), cancellationToken);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureText(string value, string field, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AppValidationException(message, new Dictionary<string, string[]> { [field] = [message] });
        }
    }
}
