using AdminPortal.Infrastructure.Persistence;
using AdminPortal.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
builder.Services.AddDbContext<AdminPortalDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddTransient<RetentionCleanup>();

using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
var cleanup = scope.ServiceProvider.GetRequiredService<RetentionCleanup>();
await cleanup.RunAsync(CancellationToken.None);
