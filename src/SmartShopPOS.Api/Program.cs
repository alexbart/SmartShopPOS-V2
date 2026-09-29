using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using SmartShopPOS.Api.Catalog;
using SmartShopPOS.Api.Inventory;
using SmartShopPOS.Application.Branches;
using SmartShopPOS.Application.Catalog;
using SmartShopPOS.Api.Branches;
using SmartShopPOS.Api.Health;
using SmartShopPOS.Application.Identity;
using SmartShopPOS.Application.Inventory;
using SmartShopPOS.Application.UserBranches;
using SmartShopPOS.Contracts.Branches;
using SmartShopPOS.Contracts.Authentication;
using SmartShopPOS.Contracts.UserBranches;
using SmartShopPOS.Infrastructure.Branches;
using SmartShopPOS.Infrastructure.Catalog;
using SmartShopPOS.Infrastructure.Identity;
using SmartShopPOS.Infrastructure.Inventory;
using SmartShopPOS.Infrastructure.Persistence;
using SmartShopPOS.Infrastructure.UserBranches;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<CookieAuthenticationOperationFilter>();
    options.AddSecurityDefinition(CookieAuthenticationOperationFilter.AuthenticationCookieScheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = "ssps_auth",
        Description = "ASP.NET Core authentication cookie."
    });
    options.AddSecurityDefinition(CookieAuthenticationOperationFilter.SessionCookieScheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Name = "ssps_session",
        Description = "Server-side session token cookie. Both authentication cookies are required."
    });
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<SmartShopPosDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IPermissionChecker, EfCorePermissionChecker>();
builder.Services.AddScoped<IIdentityWriter, EfCoreIdentityWriter>();
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<SmartShopPOS.Application.Identity.IAuthenticationService, SmartShopPOS.Infrastructure.Identity.AuthenticationService>();
builder.Services.AddScoped<ICurrentUser, CurrentUserAccessor>();
builder.Services.AddScoped<IBranchTerminalService, BranchTerminalService>();
builder.Services.AddScoped<IUserBranchAssignmentService, UserBranchAssignmentService>();
builder.Services.AddScoped<IBranchAccessService, BranchAccessService>();
builder.Services.AddScoped<IProductCatalogService, ProductCatalogService>();
builder.Services.AddScoped<IProductPricingService, ProductPricingService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();

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
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            },
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

var branches = app.MapGroup("/api/branches")
    .RequireAuthorization()
    .WithTags("Branches and terminals");

branches.MapGet("", async (IBranchTerminalService service, CancellationToken cancellationToken) =>
{
    var result = await service.GetBranchesAsync(cancellationToken);
    return result.IsSuccess ? Results.Ok(result.Value) : ToFailureResult(result.Error, result.Message);
})
.WithName("ListBranches")
.Produces<IReadOnlyList<BranchResponse>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden);

branches.MapPost("", async (CreateBranchRequest request, IBranchTerminalService service, CancellationToken cancellationToken) =>
{
    var result = await service.CreateBranchAsync(request, cancellationToken);
    return result.IsSuccess
        ? Results.Created($"/api/branches/{result.Value!.Id}", result.Value)
        : ToFailureResult(result.Error, result.Message);
})
.WithName("CreateBranch")
.Produces<BranchResponse>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status409Conflict);

branches.MapGet("/{branchId:guid}/terminals", async (Guid branchId, IBranchTerminalService service, CancellationToken cancellationToken) =>
{
    var result = await service.GetTerminalsAsync(branchId, cancellationToken);
    return result.IsSuccess ? Results.Ok(result.Value) : ToFailureResult(result.Error, result.Message);
})
.WithName("ListBranchTerminals")
.Produces<IReadOnlyList<TerminalResponse>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound);

branches.MapPost("/{branchId:guid}/terminals", async (
    Guid branchId,
    CreateTerminalRequest request,
    IBranchTerminalService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CreateTerminalAsync(branchId, request, cancellationToken);
    return result.IsSuccess
        ? Results.Created($"/api/branches/{branchId}/terminals/{result.Value!.Id}", result.Value)
        : ToFailureResult(result.Error, result.Message);
})
.WithName("CreateBranchTerminal")
.Produces<TerminalResponse>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status409Conflict);

var branchUsers = app.MapGroup("/api/branches/{branchId:guid}/users")
    .RequireAuthorization()
    .WithTags("User branch assignments");

branchUsers.MapGet("", async (
    Guid branchId,
    IUserBranchAssignmentService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.GetAssignmentsAsync(branchId, cancellationToken);
    return result.IsSuccess ? Results.Ok(result.Value) : ToUserBranchFailureResult(result.Error, result.Message);
})
.WithName("ListBranchUsers")
.Produces<IReadOnlyList<UserBranchAssignmentResponse>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound);

branchUsers.MapPost("", async (
    Guid branchId,
    AssignUserToBranchRequest request,
    IUserBranchAssignmentService service,
    CancellationToken cancellationToken) =>
{
    if (request is null)
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: "A user assignment request is required.");
    }

    var result = await service.AssignAsync(branchId, request.UserId, cancellationToken);
    return result.IsSuccess
        ? Results.Created($"/api/branches/{branchId}/users/{request.UserId}", result.Value)
        : ToUserBranchFailureResult(result.Error, result.Message);
})
.WithName("AssignUserToBranch")
.Produces<UserBranchAssignmentResponse>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status409Conflict);

branchUsers.MapDelete("/{userId:guid}", async (
    Guid branchId,
    Guid userId,
    IUserBranchAssignmentService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.DeactivateAsync(branchId, userId, cancellationToken);
    return result.IsSuccess ? Results.NoContent() : ToUserBranchFailureResult(result.Error, result.Message);
})
.WithName("DeactivateBranchUser")
.Produces(StatusCodes.Status204NoContent)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound);

var me = app.MapGroup("/api/me")
    .RequireAuthorization()
    .WithTags("Operational branch context");

me.MapGet("/branches", async (IBranchAccessService service, CancellationToken cancellationToken) =>
{
    var result = await service.GetAccessibleBranchesAsync(cancellationToken);
    return result.IsSuccess ? Results.Ok(result.Value) : ToUserBranchFailureResult(result.Error, result.Message);
})
.WithName("ListMyBranches")
.Produces<IReadOnlyList<AccessibleBranchResponse>>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden);

me.MapGet("/branch-context", async (IBranchAccessService service, CancellationToken cancellationToken) =>
{
    var result = await service.GetCurrentContextAsync(cancellationToken);
    return !result.IsSuccess
        ? ToUserBranchFailureResult(result.Error, result.Message)
        : result.Value is null ? Results.NoContent() : Results.Ok(result.Value);
})
.WithName("GetMyBranchContext")
.Produces<BranchContextResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status204NoContent)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden);

me.MapPost("/branch-context", async (
    SelectBranchContextRequest request,
    IBranchAccessService service,
    CancellationToken cancellationToken) =>
{
    if (request is null)
    {
        return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: "A branch context request is required.");
    }

    var result = await service.SelectContextAsync(request.BranchId, cancellationToken);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : ToUserBranchFailureResult(result.Error, result.Message);
})
.WithName("SelectMyBranchContext")
.Produces<BranchContextResponse>(StatusCodes.Status200OK)
.ProducesProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status403Forbidden)
.ProducesProblem(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status409Conflict);

app.MapProductCatalog();
app.MapInventory();

app.Run();

static IResult ToFailureResult(BranchTerminalError error, string? message)
{
    var (statusCode, title) = error switch
    {
        BranchTerminalError.Unauthenticated => (StatusCodes.Status401Unauthorized, "Authentication required"),
        BranchTerminalError.Forbidden => (StatusCodes.Status403Forbidden, "Permission denied"),
        BranchTerminalError.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
        BranchTerminalError.Conflict => (StatusCodes.Status409Conflict, "Resource conflict"),
        _ => (StatusCodes.Status400BadRequest, "Invalid request")
    };

    return Results.Problem(statusCode: statusCode, title: title, detail: message);
}

static IResult ToUserBranchFailureResult(UserBranchError error, string? message)
{
    var (statusCode, title) = error switch
    {
        UserBranchError.Unauthenticated => (StatusCodes.Status401Unauthorized, "Authentication required"),
        UserBranchError.Forbidden => (StatusCodes.Status403Forbidden, "Permission denied"),
        UserBranchError.NotFound => (StatusCodes.Status404NotFound, "Resource not found"),
        UserBranchError.Conflict => (StatusCodes.Status409Conflict, "Resource conflict"),
        _ => (StatusCodes.Status400BadRequest, "Invalid request")
    };

    return Results.Problem(statusCode: statusCode, title: title, detail: message);
}

public partial class Program
{
}
