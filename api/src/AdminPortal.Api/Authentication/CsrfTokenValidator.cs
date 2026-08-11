using System.Security.Cryptography;
using System.Text;
using AdminPortal.Api.Configuration;
using AdminPortal.Application.Common.Exceptions;
using Microsoft.Extensions.Options;

namespace AdminPortal.Api.Authentication;

public sealed class CsrfTokenValidator(IOptions<SecurityOptions> options)
{
    private readonly SecurityOptions _options = options.Value;

    public void Validate(HttpRequest request)
    {
        var cookieToken = request.Cookies[_options.CsrfCookieName];
        var headerToken = request.Headers[_options.CsrfHeaderName].ToString();
        if (string.IsNullOrWhiteSpace(cookieToken) || string.IsNullOrWhiteSpace(headerToken))
        {
            throw new ForbiddenException("Thiếu CSRF token.");
        }

        var cookieBytes = Encoding.UTF8.GetBytes(cookieToken);
        var headerBytes = Encoding.UTF8.GetBytes(headerToken);
        if (cookieBytes.Length != headerBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(cookieBytes, headerBytes))
        {
            throw new ForbiddenException("CSRF token không hợp lệ.");
        }
    }
}
