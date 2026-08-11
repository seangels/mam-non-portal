namespace AdminPortal.Api.Configuration;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";
    public string[] AllowedOrigins { get; init; } = [];
    public string RefreshCookieName { get; init; } = "refresh_token";
    public string CsrfCookieName { get; init; } = "XSRF-TOKEN";
    public string CsrfHeaderName { get; init; } = "X-CSRF-TOKEN";
}
