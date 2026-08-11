using AdminPortal.Application.Common.Exceptions;

namespace AdminPortal.Application.Common;

public static class PasswordPolicy
{
    public static void Validate(string password)
    {
        var errors = new List<string>();
        if (password.Length is < 12 or > 128) errors.Add("Mật khẩu phải dài từ 12 đến 128 ký tự.");
        if (!password.Any(char.IsUpper)) errors.Add("Mật khẩu phải có chữ hoa.");
        if (!password.Any(char.IsLower)) errors.Add("Mật khẩu phải có chữ thường.");
        if (!password.Any(char.IsDigit)) errors.Add("Mật khẩu phải có chữ số.");
        if (!password.Any(character => !char.IsLetterOrDigit(character))) errors.Add("Mật khẩu phải có ký tự đặc biệt.");
        if (errors.Count > 0)
        {
            throw new AppValidationException("Mật khẩu không đáp ứng chính sách bảo mật.", new Dictionary<string, string[]>
            {
                ["password"] = errors.ToArray()
            });
        }
    }
}
