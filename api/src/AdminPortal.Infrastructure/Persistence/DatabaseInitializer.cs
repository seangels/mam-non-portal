using AdminPortal.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AdminPortal.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var databaseOptions = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var dbContext = scope.ServiceProvider.GetRequiredService<AdminPortalDbContext>();
        if (databaseOptions.MigrateOnStartup)
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

    }
}
