using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartShopPOS.Api.Health;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Contracts.Authentication;
using SmartShopPOS.Infrastructure.Identity;
using SmartShopPOS.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<SmartShopPosDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPermissionChecker, EfCorePermissionChecker>();
builder.Services.AddScoped<IIdentityWriter, EfCoreIdentityWriter>();
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<SmartShopPOS.Application.Identity.IAuthenticationService, SmartShopPOS.Infrastructure.Identity.AuthenticationService>();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ssps_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/api/auth/login";
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var principal = context.Principal ?? new ClaimsPrincipal();
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<SmartShopPosDbContext>();
                var sessionIdValue = principal.FindFirst("session_id")?.Value;
                var sessionTokenValue = context.HttpContext.Request.Cookies["ssps_session"];

                if (string.IsNullOrWhiteSpace(sessionIdValue) || string.IsNullOrWhiteSpace(sessionTokenValue))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                if (!Guid.TryParse(sessionIdValue, out var sessionId))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                var session = await dbContext.AuthenticationSessions
                    .SingleOrDefaultAsync(item => item.SessionId == sessionId, context.HttpContext.RequestAborted);

                if (session is null || session.IsExpired(DateTimeOffset.UtcNow))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                var expectedHash = AuthenticationSecurity.HashToken(sessionTokenValue);
                if (!string.Equals(session.TokenHash, expectedHash, StringComparison.Ordinal))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                session.Touch(DateTimeOffset.UtcNow);
                await dbContext.SaveChangesAsync(context.HttpContext.RequestAborted);
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql");

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Name == "postgresql"
});
app.MapHealthChecks("/health");

app.MapPost("/api/auth/login", async (LoginRequest request, SmartShopPOS.Application.Identity.IAuthenticationService authenticationService, HttpContext httpContext) =>
{
    try
    {
        var authenticatedUser = await authenticationService.LoginAsync(request);
        return Results.Ok(authenticatedUser);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
}).AllowAnonymous().RequireRateLimiting("login");

app.MapGet("/api/auth/me", async (SmartShopPOS.Application.Identity.IAuthenticationService authenticationService) =>
{
    var currentUser = await authenticationService.GetCurrentUserAsync();
    return currentUser is null ? Results.Unauthorized() : Results.Ok(currentUser);
});

app.MapPost("/api/auth/logout", async (SmartShopPOS.Application.Identity.IAuthenticationService authenticationService, HttpContext httpContext) =>
{
    await authenticationService.LogoutAsync();
    httpContext.Response.Cookies.Delete("ssps_session");
    return Results.Ok();
});

app.Run();

public partial class Program
{
}
