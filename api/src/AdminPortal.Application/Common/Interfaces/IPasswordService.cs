using AdminPortal.Domain.Entities;

namespace AdminPortal.Application.Common.Interfaces;

public interface IPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string passwordHash, string providedPassword);
}
