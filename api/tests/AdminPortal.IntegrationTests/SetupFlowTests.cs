using System.Net;
using System.Net.Http.Json;
using AdminPortal.Application.Setup;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AdminPortal.IntegrationTests;

public sealed class SetupFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task EmptyDatabaseAllowsExactlyOneSuperAdminInitialization()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();

        var initialStatus = await firstClient.GetFromJsonAsync<SetupStatusResponse>("/api/v1/setup/status");
        Assert.True(initialStatus?.RequiresInitialization);

        var firstRequest = firstClient.PostAsJsonAsync("/api/v1/setup/super-admin", new
        {
            email = ApiFactory.SuperAdminEmail,
            fullName = "Integration SuperAdmin",
            password = ApiFactory.SuperAdminPassword
        });
        var secondRequest = secondClient.PostAsJsonAsync("/api/v1/setup/super-admin", new
        {
            email = "second-superadmin@example.test",
            fullName = "Second SuperAdmin",
            password = "StrongSetupPassword2!"
        });

        var responses = await Task.WhenAll(firstRequest, secondRequest);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);

        var completedStatus = await firstClient.GetFromJsonAsync<SetupStatusResponse>("/api/v1/setup/status");
        Assert.False(completedStatus?.RequiresInitialization);

        var winningEmail = responses[0].StatusCode == HttpStatusCode.Created
            ? ApiFactory.SuperAdminEmail
            : "second-superadmin@example.test";
        var winningPassword = responses[0].StatusCode == HttpStatusCode.Created
            ? ApiFactory.SuperAdminPassword
            : "StrongSetupPassword2!";
        var login = await firstClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = winningEmail, password = winningPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private HttpClient CreateClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = true
    });
}
