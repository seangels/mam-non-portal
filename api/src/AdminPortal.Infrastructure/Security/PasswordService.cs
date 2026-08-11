using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace AdminPortal.Infrastructure.Security;

public sealed class PasswordService(IPasswordHasher<User> passwordHasher) : IPasswordService
{
    public string Hash(User user, string password) => passwordHasher.HashPassword(user, password);

    public bool Verify(User user, string passwordHash, string providedPassword) =>
        passwordHasher.VerifyHashedPassword(user, passwordHash, providedPassword) != PasswordVerificationResult.Failed;
}
