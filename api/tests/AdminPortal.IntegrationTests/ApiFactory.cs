using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace AdminPortal.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SuperAdminEmail = "superadmin@example.test";
    public const string SuperAdminPassword = "StrongSetupPassword1!";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("admin_portal_tests")
        .WithUsername("admin_portal")
        .WithPassword("integration-test-password")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        return _postgres.DisposeAsync().AsTask();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Database:MigrateOnStartup"] = "true",
                ["Jwt:SigningKey"] = "integration-test-signing-key-that-is-longer-than-thirty-two-characters",
                ["Security:AllowedOrigins:0"] = "https://ui.example.test"
            }));
    }
}
