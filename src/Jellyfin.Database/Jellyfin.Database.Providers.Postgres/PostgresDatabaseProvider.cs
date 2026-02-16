using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.DbConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Jellyfin.Database.Providers.Postgres;

/// <summary>
/// Configures jellyfin to use a PostgreSQL database.
/// </summary>
[JellyfinDatabaseProviderKey(DatabaseType)]
public sealed class PostgresDatabaseProvider : IJellyfinDatabaseProvider
{
    /// <summary>
    /// The provider key used by database config.
    /// </summary>
    public const string DatabaseType = "Jellyfin-Postgres";

    private readonly ILogger<PostgresDatabaseProvider> _logger;
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgresDatabaseProvider"/> class.
    /// </summary>
    /// <param name="logger">A logger.</param>
    public PostgresDatabaseProvider(ILogger<PostgresDatabaseProvider> logger)
    {
        _logger = logger;
        _connectionString = BuildConnectionStringFromEnvironment();
    }

    /// <inheritdoc/>
    public IDbContextFactory<JellyfinDbContext>? DbContextFactory { get; set; }

    /// <inheritdoc/>
    public void Initialise(DbContextOptionsBuilder options, DatabaseConfigurationOptions databaseConfiguration)
    {
        _logger.LogInformation("Using PostgreSQL database provider from DATABASE_URL.");

        options
            .UseNpgsql(
                _connectionString,
                npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null))
            .UseSnakeCaseNamingConvention();
    }

    /// <inheritdoc/>
    public Task RunScheduledOptimisation(CancellationToken cancellationToken)
        => ExecuteRawSql("VACUUM ANALYZE", cancellationToken);

    /// <inheritdoc/>
    public Task RunShutdownTask(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<string> MigrationBackupFast(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Migration backup is not implemented for PostgreSQL provider. External database backups are recommended.");
        return Task.FromResult(string.Empty);
    }

    /// <inheritdoc/>
    public Task RestoreBackupFast(string key, CancellationToken cancellationToken)
    {
        _logger.LogWarning("RestoreBackupFast was requested for PostgreSQL provider and is not implemented.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteBackup(string key)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task PurgeDatabase(JellyfinDbContext dbContext, IEnumerable<string>? tableNames)
    {
        ArgumentNullException.ThrowIfNull(tableNames);

        var escapedNames = new List<string>();
        foreach (var tableName in tableNames)
        {
            escapedNames.Add($"\"{tableName.Replace("\"", "\"\"", StringComparison.Ordinal)}\"");
        }

        if (escapedNames.Count == 0)
        {
            return Task.CompletedTask;
        }

        return dbContext.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {string.Join(", ", escapedNames)} RESTART IDENTITY CASCADE;");
    }

    /// <inheritdoc/>
    public void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.SetDefaultDateTimeKind(DateTimeKind.Utc);
    }

    /// <inheritdoc/>
    public void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
    }

    private async Task ExecuteRawSql(string sql, CancellationToken cancellationToken)
    {
        if (DbContextFactory is null)
        {
            return;
        }

        var context = await DbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (context.ConfigureAwait(false))
        {
            await context.Database.ExecuteSqlRawAsync(sql, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string BuildConnectionStringFromEnvironment()
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(databaseUrl))
        {
            throw new InvalidOperationException("DATABASE_URL is required when using the Jellyfin-Postgres database provider.");
        }

        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("DATABASE_URL is not a valid absolute URI.");
        }

        var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.TrimEntries);
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty;
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = username,
            Password = password,
            Pooling = true,
            Timeout = 15,
            CommandTimeout = 30,
            MaxPoolSize = GetIntEnv("DATABASE_MAX_POOL_SIZE", 20),
            MinPoolSize = GetIntEnv("DATABASE_MIN_POOL_SIZE", 0)
        };

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in query)
        {
            var split = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (split.Length != 2)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(split[0]);
            var value = Uri.UnescapeDataString(split[1]);
            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase) && Enum.TryParse<SslMode>(value, true, out var sslMode))
            {
                builder.SslMode = sslMode;
            }

            if (key.Equals("trustservercertificate", StringComparison.OrdinalIgnoreCase)
                && bool.TryParse(value, out var trustServerCertificate))
            {
                builder.TrustServerCertificate = trustServerCertificate;
            }
        }

        return builder.ConnectionString;
    }

    private static int GetIntEnv(string key, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(key), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
