namespace AdminPortal.Api.Configuration;

public sealed class SpaOptions
{
    public const string SectionName = "Spa";

    public bool ServeFromClientAppBuild { get; init; }

    public string BuildPath { get; init; } = "ClientApp/build";
}
