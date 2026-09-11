namespace AdminPortal.Api.Configuration;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";
    public string[] AllowedOrigins { get; init; } = [];
}
