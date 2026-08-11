using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AdminPortal.Application.Common;
using AdminPortal.Api.Authentication;
using AdminPortal.Api.Configuration;
using AdminPortal.Api.Infrastructure;
using AdminPortal.Infrastructure;
using AdminPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AdminPortal.Application.Common.Interfaces.ICurrentActor, HttpCurrentActor>();
builder.Services.AddScoped<CsrfTokenValidator>();
builder.Services.AddAdminPortalAuthentication();
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Dữ liệu không hợp lệ",
            Type = "https://httpstatuses.com/400"
        };
        problem.Extensions["code"] = ProblemCodes.ValidationFailed;
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(problem);
    };
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready"]);
var loginPermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 5;
var refreshPermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 20;
var setupPermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 5;
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth-login", context => CreateFixedWindowPartition(context, loginPermitLimit));
    options.AddPolicy("auth-refresh", context => CreateFixedWindowPartition(context, refreshPermitLimit));
    options.AddPolicy("setup", context => CreateFixedWindowPartition(context, setupPermitLimit));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Quá nhiều yêu cầu",
            Detail = "Vui lòng thử lại sau.",
            Type = "https://httpstatuses.com/429"
        }, cancellationToken);
    };
});

var securityOptions = builder.Configuration
    .GetSection(SecurityOptions.SectionName)
    .Get<SecurityOptions>() ?? new SecurityOptions();
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (securityOptions.AllowedOrigins.Length > 0)
    {
        policy.WithOrigins(securityOptions.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    }
}));

var app = builder.Build();

await app.Services.InitializeDatabaseAsync(app.Lifetime.ApplicationStopping);

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

await app.RunAsync();

static RateLimitPartition<string> CreateFixedWindowPartition(HttpContext context, int permitLimit) =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = permitLimit,
            QueueLimit = 0,
            Window = TimeSpan.FromMinutes(1)
        });

public partial class Program;
