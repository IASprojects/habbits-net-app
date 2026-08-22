using HabitsApp.Api.Services;
using HabitsApp.Application.Contracts;
using HabitsApp.Application.Contracts.Auth;
using HabitsApp.Application.Contracts.Habits;
using HabitsApp.Application.Services;
using HabitsApp.Domain.Entities;
using HabitsApp.Infrastructure.Abstractions;
using HabitsApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddOpenApi();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IDatabaseHealthService, DatabaseHealthService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IHabitService, HabitService>();

// Configure ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthorization();

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var secretKey = builder.Configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JwtSettings:SecretKey not found in configuration.");
    var key = Encoding.UTF8.GetBytes(secretKey);

    options.MapInboundClaims = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "HabitsApp",
        ValidAudience = builder.Configuration["JwtSettings:Audience"] ?? "HabitsApp",
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
});

// Register JWT settings and auth service
builder.Services.AddSingleton<IJwtSettings>(_ =>
    builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JwtSettings configuration is missing."));
builder.Services.AddScoped<IAuthService, AuthService>();

// Enable .NET 10 native Minimal API validation (data annotations)
builder.Services.AddValidation();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebBlazor",
        policy => policy
            .WithOrigins(
                "http://localhost:5119",
                "https://localhost:7243")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("WebBlazor");

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/auth/register", async (RegisterUserCommand command, IAuthService authService, CancellationToken cancellationToken) =>
{
    var result = await authService.RegisterAsync(command, cancellationToken);
    if (result.Succeeded)
    {
        return Results.Created("/api/auth/me", result.Response);
    }

    return Results.Problem(
        statusCode: result.StatusCode ?? StatusCodes.Status400BadRequest,
        title: result.ErrorType,
        detail: result.ErrorDetail,
        extensions: result.ValidationErrors is { Count: > 0 }
            ? new Dictionary<string, object?> { ["errors"] = result.ValidationErrors }
            : null);
});

app.MapPost("/api/auth/login", async (LoginCommand command, IAuthService authService, CancellationToken cancellationToken) =>
{
    var result = await authService.LoginAsync(command, cancellationToken);
    if (result.Succeeded)
    {
        return Results.Ok(result.Response);
    }

    return Results.Problem(
        statusCode: result.StatusCode ?? StatusCodes.Status401Unauthorized,
        title: result.ErrorType,
        detail: result.ErrorDetail);
});

app.MapPost("/api/auth/refresh", async (RefreshTokenCommand command, IAuthService authService, CancellationToken cancellationToken) =>
{
    var result = await authService.RefreshAsync(command, cancellationToken);
    if (result.Succeeded)
    {
        return Results.Ok(result.Response);
    }

    return Results.Problem(
        statusCode: result.StatusCode ?? StatusCodes.Status401Unauthorized,
        title: result.ErrorType,
        detail: result.ErrorDetail);
});

app.MapPost("/api/auth/logout", async (LogoutCommand command, IAuthService authService, CancellationToken cancellationToken) =>
{
    var result = await authService.LogoutAsync(command, cancellationToken);
    if (!result.Succeeded)
    {
        return Results.Problem(
            statusCode: result.StatusCode ?? StatusCodes.Status400BadRequest,
            title: result.ErrorType,
            detail: result.ErrorDetail);
    }

    return Results.NoContent();
})
.RequireAuthorization();

app.MapGet("/api/auth/me", async (ClaimsPrincipal principal, IAuthService authService, CancellationToken cancellationToken) =>
{
    var profile = await authService.GetCurrentUserAsync(principal, cancellationToken);
    return profile is null ? Results.Unauthorized() : Results.Ok(profile);
})
.RequireAuthorization();

app.MapGet("/api/health", async (IDatabaseHealthService healthService) =>
{
    var start = DateTime.UtcNow;
    var isConnected = await healthService.CheckDatabaseHealthAsync();
    var latency = (DateTime.UtcNow - start).TotalMilliseconds;

    return Results.Ok(new
    {
        IsConnected = isConnected,
        DatabaseName = "HabitsApp",
        TimestampUtc = DateTime.UtcNow.ToString("o"),
        LatencyMs = latency
    });
});

static Guid GetUserId(ClaimsPrincipal principal)
{
    var sub = principal.FindFirstValue("sub");
    return sub is not null && Guid.TryParse(sub, out var userId) ? userId : Guid.Empty;
}

var habitsGroup = app.MapGroup("/api/habits")
    .RequireAuthorization();

habitsGroup.MapGet("/", async (ClaimsPrincipal principal, IHabitService habitService, CancellationToken cancellationToken) =>
{
    var items = await habitService.GetDashboardAsync(GetUserId(principal), cancellationToken);
    return Results.Ok(items);
});

habitsGroup.MapPost("/", async (CreateHabitDto dto, ClaimsPrincipal principal, IHabitService habitService, CancellationToken cancellationToken) =>
{
    var result = await habitService.CreateAsync(GetUserId(principal), dto, cancellationToken);
    if (result.Succeeded)
    {
        return Results.Created($"/api/habits/{result.Data!.Id}", result.Data);
    }

    return Results.Problem(
        statusCode: result.StatusCode ?? StatusCodes.Status400BadRequest,
        title: result.ErrorType,
        detail: result.ErrorDetail);
});

habitsGroup.MapPut("/{id:guid}", async (Guid id, UpdateHabitDto dto, ClaimsPrincipal principal, IHabitService habitService, CancellationToken cancellationToken) =>
{
    var result = await habitService.UpdateAsync(GetUserId(principal), id, dto, cancellationToken);
    if (result.Succeeded)
    {
        return Results.Ok(result.Data);
    }

    return Results.Problem(
        statusCode: result.StatusCode ?? StatusCodes.Status400BadRequest,
        title: result.ErrorType,
        detail: result.ErrorDetail);
});

habitsGroup.MapPost("/{id:guid}/quick-log", async (Guid id, ClaimsPrincipal principal, IHabitService habitService, CancellationToken cancellationToken) =>
{
    var result = await habitService.QuickLogAsync(GetUserId(principal), id, cancellationToken);
    if (result.Succeeded)
    {
        return Results.Ok(result.Data);
    }

    return Results.Problem(
        statusCode: result.StatusCode ?? StatusCodes.Status400BadRequest,
        title: result.ErrorType,
        detail: result.ErrorDetail);
});

habitsGroup.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, IHabitService habitService, CancellationToken cancellationToken) =>
{
    var result = await habitService.ArchiveAsync(GetUserId(principal), id, cancellationToken);
    if (result.Succeeded)
    {
        return Results.NoContent();
    }

    return Results.Problem(
        statusCode: result.StatusCode ?? StatusCodes.Status400BadRequest,
        title: result.ErrorType,
        detail: result.ErrorDetail);
});

app.Run();