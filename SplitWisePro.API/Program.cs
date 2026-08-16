using System.Text;
using Microsoft.EntityFrameworkCore;
using SplitWisePro.Infrastructure.Data;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SplitWisePro.API.Hubs;
using SplitWisePro.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Infrastructure Layer (DbContext, Repositories, UoW, Services) ──
builder.Services.AddInfrastructure(builder.Configuration);

// ── Controllers with JSON enum support ─────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// ── JWT Authentication ─────────────────────────────────────────────
var jwtKey = builder.Configuration["JwtSettings:SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    // Allow SignalR to receive tokens via query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ── Rate Limiting (keyed by User ID, not just IP) ──────────────────
builder.Services.AddRateLimiter(options =>
{
    var permitLimit = int.Parse(builder.Configuration["RateLimiting:PermitLimit"] ?? "100");
    var windowSeconds = int.Parse(builder.Configuration["RateLimiting:WindowInSeconds"] ?? "60");
    var queueLimit = int.Parse(builder.Configuration["RateLimiting:QueueLimit"] ?? "10");

    // Authenticated user policy — keyed by UserId
    options.AddPolicy("authenticated", httpContext =>
    {
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromSeconds(windowSeconds),
            QueueLimit = queueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    // Stricter policy for auth endpoints (prevent brute force)
    options.AddPolicy("auth", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"auth_{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 2,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    // Global fallback
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 200,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 20,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Rate limit exceeded. Please try again later.\"}",
            cancellationToken: ct);
    };
});

// ── SignalR ─────────────────────────────────────────────────────────
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 64 * 1024; // 64KB
});

// ── CORS ───────────────────────────────────────────────────────────
// Using SetIsOriginAllowed instead of WithOrigins so:
// 1. Any *.vercel.app frontend URL works (including samatva-one.vercel.app)
// 2. Render environment variable overrides don't break things
// 3. Vercel preview deployments work automatically
builder.Services.AddCors(options =>
{
    options.AddPolicy("SplitWiseProCors", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrEmpty(origin)) return false;
            var uri = new Uri(origin);
            // Allow any Vercel deployment
            if (uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)) return true;
            // Allow localhost for dev
            if (uri.Host == "localhost" || uri.Host == "127.0.0.1") return true;
            // Allow Capacitor/Ionic for mobile
            if (origin.StartsWith("capacitor://") || origin.StartsWith("ionic://")) return true;
            return false;
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials(); // Required for SignalR
    });
});

var app = builder.Build();

// ── Startup Diagnostics ───────────────────────────────────────────
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();

// Log environment
startupLogger.LogInformation("=== SAMATVA STARTUP ===");
startupLogger.LogInformation("Environment: {Env}", app.Environment.EnvironmentName);

// Log connection string (masked)
var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "NOT SET";
var maskedConn = connStr.Length > 20 ? connStr[..20] + "..." : connStr;
startupLogger.LogInformation("DB ConnectionString: {Conn}", maskedConn);

// Log JWT config
startupLogger.LogInformation("JWT Issuer: {Issuer}", builder.Configuration["JwtSettings:Issuer"] ?? "NOT SET");
startupLogger.LogInformation("JWT Audience: {Audience}", builder.Configuration["JwtSettings:Audience"] ?? "NOT SET");
startupLogger.LogInformation("JWT Key length: {Len}", builder.Configuration["JwtSettings:SecretKey"]?.Length ?? 0);

// ── Auto-migrate Database on Startup ──────────────────────────────
try
{
    startupLogger.LogInformation("Starting database migration...");
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    startupLogger.LogInformation("Database migration completed successfully.");
}
catch (Exception ex)
{
    startupLogger.LogError(ex, "DATABASE MIGRATION FAILED: {Message}", ex.Message);
    // Don't crash — app can still serve health checks
}

// ── Middleware Pipeline ────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// NOTE: No UseHttpsRedirection — Render serves on HTTP (port 10000), HTTPS is handled by their proxy.
// HttpsRedirection would break CORS preflight requests on Render.
app.UseCors("SplitWiseProCors");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ── SignalR Hub Endpoint ───────────────────────────────────────────
app.MapHub<ExpenseHub>("/hubs/expenses");

// ── Health Check Endpoint ──────────────────────────────────────────
app.MapGet("/api/health", (ILogger<Program> logger) =>
{
    logger.LogInformation("Health check called.");
    return Results.Ok(new
    {
        Status = "Healthy",
        Timestamp = DateTime.UtcNow,
        Version = "1.0.1",
        Environment = app.Environment.EnvironmentName
    });
});

// ── CORS Debug Endpoint ────────────────────────────────────────────
app.MapGet("/api/cors-test", (HttpContext ctx) =>
{
    var origin = ctx.Request.Headers.Origin.ToString();
    return Results.Ok(new { Message = "CORS OK", YourOrigin = origin, Timestamp = DateTime.UtcNow });
});

app.Run();
