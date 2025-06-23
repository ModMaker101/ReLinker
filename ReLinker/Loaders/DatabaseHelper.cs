using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.Logging;
using Npgsql;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;
using System.Data.SQLite;
using Oracle.ManagedDataAccess.Client;
using FirebirdSql.Data.FirebirdClient;

namespace ReLinker
{
    public static class RetryHelper
    {
        public static T RetryWithBackoff<T>(Func<T> operation, int maxRetries = 3, int delayMilliseconds = 500)
        {
            int retries = 0;
            while (true)
            {
                try
                {
                    return operation();
                }
                catch (Exception ex)
                {
                    if (retries >= maxRetries)
                        throw new Exception($"Operation failed after {maxRetries} retries.", ex);
                    Thread.Sleep(delayMilliseconds * (int)Math.Pow(2, retries));
                    retries++;
                }
            }
        }
    }

    public interface IDatabaseLoader
    {
        Task<List<Record>> LoadRecordsAsync();
        IAsyncEnumerable<Record> LoadRecordsInBatchesAsync(int batchSize, int startOffset = 0);
        List<Record> LoadRecords();
        IEnumerable<Record> LoadRecordsInBatches(int batchSize, int startOffset = 0);
    }

    public class GenericDbLoader : IDatabaseLoader
    {
        private readonly string _providerName;
        private readonly string _connectionString;
        private readonly string _query;
        private readonly ILogger<GenericDbLoader> _logger;

        public GenericDbLoader(string providerName, string connectionString, string query, ILogger<GenericDbLoader> logger)
        {
            _providerName = providerName;
            _connectionString = connectionString;
            _query = query;
            _logger = logger;

            var registered = DbProviderFactories.GetFactoryClasses()
                .Rows.Cast<System.Data.DataRow>()
                .Select(r => r["InvariantName"].ToString())
                .ToHashSet();

            if (!registered.Contains(providerName))
            {
                try
                {
                    switch (providerName)
                    {
                        case "Npgsql":
                            DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
                            break;
                        case "Microsoft.Data.SqlClient":
                            DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);
                            break;
                        case "MySql.Data.MySqlClient":
                            DbProviderFactories.RegisterFactory("MySql.Data.MySqlClient", MySqlClientFactory.Instance);
                            break;
                        case "System.Data.SQLite":
                            DbProviderFactories.RegisterFactory("System.Data.SQLite", SQLiteFactory.Instance);
                            break;
                        case "Oracle.ManagedDataAccess.Client":
                            DbProviderFactories.RegisterFactory("Oracle.ManagedDataAccess.Client", OracleClientFactory.Instance);
                            break;
                        case "FirebirdSql.Data.FirebirdClient":
                            DbProviderFactories.RegisterFactory("FirebirdSql.Data.FirebirdClient", FirebirdClientFactory.Instance);
                            break;
                        default:
                            throw new NotSupportedException($"Provider '{providerName}' is not supported for auto-registration.");
                    }
                    _logger.LogInformation("[GenericDbLoader] Registered provider '{ProviderName}' automatically.", providerName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[GenericDbLoader] Failed to register provider '{ProviderName}': {Message}", providerName, ex.Message);
                    throw;
                }
            }
        }

        public async Task<List<Record>> LoadRecordsAsync()
        {
            var records = new List<Record>();
            try
            {
                var factory = DbProviderFactories.GetFactory(_providerName);
                using var connection = factory.CreateConnection();
                connection.ConnectionString = _connectionString;
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = _query;
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetInt32(0).ToString();
                    var fields = new Dictionary<string, string>();
                    for (int i = 1; i < reader.FieldCount; i++)
                        fields[reader.GetName(i)] = reader[i]?.ToString() ?? "";
                    records.Add(new Record(id, fields));
                }
                _logger.LogInformation("[GenericDbLoader] Loaded {Count} records asynchronously.", records.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GenericDbLoader] Error loading records asynchronously: {Message}", ex.Message);
            }
            return records;
        }

        public async IAsyncEnumerable<Record> LoadRecordsInBatchesAsync(int batchSize, int startOffset = 0)
        {
            var records = await LoadRecordsAsync();
            for (int i = startOffset; i < records.Count; i += batchSize)
            {
                var batch = records.Skip(i).Take(batchSize);
                foreach (var record in batch)
                    yield return record;
                await Task.Yield();
            }
        }

        public List<Record> LoadRecords()
        {
            var records = new List<Record>();
            try
            {
                var factory = DbProviderFactories.GetFactory(_providerName);
                using var connection = factory.CreateConnection();
                connection.ConnectionString = _connectionString;
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = _query;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var id = reader.GetInt32(0).ToString();
                    var fields = new Dictionary<string, string>();
                    for (int i = 1; i < reader.FieldCount; i++)
                        fields[reader.GetName(i)] = reader[i]?.ToString() ?? "";
                    records.Add(new Record(id, fields));
                }
                _logger.LogInformation("[GenericDbLoader] Loaded {Count} records synchronously.", records.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GenericDbLoader] Error loading records synchronously: {Message}", ex.Message);
            }
            return records;
        }

        public IEnumerable<Record> LoadRecordsInBatches(int batchSize, int startOffset = 0)
        {
            var batch = new List<Record>();
            try
            {
                var paginatedQuery = $"{_query} OFFSET {startOffset} LIMIT {batchSize}";
                var factory = DbProviderFactories.GetFactory(_providerName);
                using var connection = factory.CreateConnection();
                connection.ConnectionString = _connectionString;
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = paginatedQuery;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var id = reader.GetInt32(0).ToString();
                    var fields = new Dictionary<string, string>();
                    for (int i = 1; i < reader.FieldCount; i++)
                        fields[reader.GetName(i)] = reader[i]?.ToString() ?? "";
                    batch.Add(new Record(id, fields));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GenericDbLoader] Error loading records in paginated batch: {Message}", ex.Message);
            }
            foreach (var record in batch)
                yield return record;
            _logger.LogInformation("[GenericDbLoader] Yielded {Count} records in paginated batch.", batch.Count);
        }
    }

    public static class DatabaseLoaderFactory
    {
        public static IDatabaseLoader CreateGenericLoader(string providerName, string connectionString, string query, ILogger<GenericDbLoader> logger)
            => new GenericDbLoader(providerName, connectionString, query, logger);

        public static IDatabaseLoader CreateDuckDbLoader(string connectionString, string query, ILogger<DuckDbLoader> logger)
            => new DuckDbLoader(connectionString, query, logger);

        public static IDatabaseLoader CreateRavenDbLoader(string url, string database, string collection, ILogger<RavenDbLoader> logger)
            => new RavenDbLoader(url, database, collection, logger);
    }
}
