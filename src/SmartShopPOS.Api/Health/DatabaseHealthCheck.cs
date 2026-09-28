using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace SmartShopPOS.Api.Health;

public sealed class DatabaseHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "PostgreSQL connection is not configured. Set ConnectionStrings:DefaultConnection to a valid local connection string."));
        }

        if (connectionString.Contains("your_", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "PostgreSQL configuration is still pending. Replace the placeholder values in ConnectionStrings:DefaultConnection with local database credentials."));
        }

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.OpenAsync(cancellationToken).GetAwaiter().GetResult();
            return Task.FromResult(HealthCheckResult.Healthy("PostgreSQL connection is healthy."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "PostgreSQL connection check failed.", ex));
        }
    }
}
