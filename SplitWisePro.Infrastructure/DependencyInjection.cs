using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SplitWisePro.Core.Interfaces;
using SplitWisePro.Infrastructure.Data;
using SplitWisePro.Infrastructure.Repositories;
using SplitWisePro.Infrastructure.Services;

namespace SplitWisePro.Infrastructure;

/// <summary>
/// Extension methods for registering Infrastructure services (DbContext, repositories)
/// into the DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // ── DbContext Registration ─────────────────────────────────────
        // Uses PostgreSQL for production (Render) and local dev.
        // Set USE_SQL_SERVER=true in environment to override to SQL Server locally.
        var connStr = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");

        var useSqlServer = string.Equals(
            configuration["USE_SQL_SERVER"], "true",
            StringComparison.OrdinalIgnoreCase);

        services.AddDbContext<AppDbContext>(options =>
        {
            if (useSqlServer)
            {
                options.UseSqlServer(connStr, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                    sqlOptions.CommandTimeout(30);
                });
            }
            else
            {
                options.UseNpgsql(connStr, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
                    npgsqlOptions.CommandTimeout(30);
                });
            }

            // Enable detailed errors only in development
            #if DEBUG
            options.EnableDetailedErrors();
            options.EnableSensitiveDataLogging();
            #endif
        });

        // ── Repository Registrations ───────────────────────────────────
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Service Registrations ──────────────────────────────────────
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAiParserService, AiParserService>();
        services.AddScoped<IEmailService, SmtpEmailService>();

        return services;
    }
}
