using AdminPortal.Application.Common.Interfaces;
using AdminPortal.Application.Auth;
using AdminPortal.Application.Users;
using AdminPortal.Application.Students;
using AdminPortal.Infrastructure.Options;
using AdminPortal.Infrastructure.Persistence;
using AdminPortal.Infrastructure.Security;
using AdminPortal.Infrastructure.Setup;
using AdminPortal.Application.Setup;
using AdminPortal.Domain.Entities;
using AdminPortal.Application.Teachers;
using AdminPortal.Application.StudentGroups;
using AdminPortal.Application.Attendance;
using AdminPortal.Infrastructure.Attendance;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AdminPortal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AdminPortalDbContext>((provider, options) =>
        {
            var currentConfiguration = provider.GetRequiredService<IConfiguration>();
            var connectionString = currentConfiguration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

            options.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsAssembly(typeof(AdminPortalDbContext).Assembly.FullName))
                .UseSnakeCaseNamingConvention();
        });
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AdminPortalDbContext>());
        services.AddScoped<IDatabaseExceptionClassifier, PostgresExceptionClassifier>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<UserAccountCoordinator>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<ITeacherService, TeacherService>();
        services.AddScoped<IStudentGroupService, StudentGroupService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IAttendancePersistence, AttendancePersistence>();
        services.AddScoped<IBusinessDateProvider, BusinessDateProvider>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddSingleton(TimeProvider.System);

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(options => options.SigningKey.Length >= 32, "Jwt:SigningKey must contain at least 32 characters.")
            .Validate(options => options.AccessTokenMinutes is > 0 and <= 60, "Jwt:AccessTokenMinutes must be between 1 and 60.")
            .Validate(options => options.RefreshTokenDays is > 0 and <= 90, "Jwt:RefreshTokenDays must be between 1 and 90.")
            .ValidateOnStart();
        services.AddOptions<AttendanceOptions>()
            .Bind(configuration.GetSection(AttendanceOptions.SectionName));

        return services;
    }
}
