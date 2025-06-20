
using DuckDB.NET.Data;
using Raven.Client.Documents;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace RecordLink
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
    //hello
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

        public GenericDbLoader(string providerName, string connectionString, string query)
        {
            _providerName = providerName;
            _connectionString = connectionString;
            _query = query;
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

                SimpleLogger.Info($"[GenericDbLoader] Loaded {records.Count} records asynchronously.");
            }
            catch (Exception ex)
            {
                SimpleLogger.Error($"[GenericDbLoader] Error loading records asynchronously: {ex.Message}");
            }

            return records;
        }

        public async IAsyncEnumerable<Record> LoadRecordsInBatchesAsync(int batchSize, int startOffset = 0)
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
            }
            catch (Exception ex)
            {
                SimpleLogger.Error($"[GenericDbLoader] Error loading records in batches asynchronously: {ex.Message}");
                yield break;
            }

            // Yield records in batches outside the try-catch
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

                SimpleLogger.Info($"[GenericDbLoader] Loaded {records.Count} records synchronously.");
            }
            catch (Exception ex)
            {
                SimpleLogger.Error($"[GenericDbLoader] Error loading records synchronously: {ex.Message}");
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
                SimpleLogger.Error($"[GenericDbLoader] Error loading records in paginated batch: {ex.Message}");
            }

            foreach (var record in batch)
                yield return record;

            SimpleLogger.Info($"[GenericDbLoader] Yielded {batch.Count} records in paginated batch.");
        }


        public class DuckDbLoader : IDatabaseLoader
        {
            private readonly string _connectionString;
            private readonly string _query;

            public DuckDbLoader(string connectionString, string query)
            {
                _connectionString = connectionString;
                _query = query;
            }

            public List<Record> LoadRecords()
            {
                var records = new List<Record>();
                try
                {
                    using var connection = new DuckDBConnection(_connectionString);
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
                    SimpleLogger.Info($"[DuckDbLoader] Loaded {records.Count} records.");
                }
                catch (Exception ex)
                {
                    SimpleLogger.Error($"[DuckDbLoader] Error loading records: {ex.Message}");
                }

                return records;
            }

            public IEnumerable<Record> LoadRecordsInBatches(int batchSize, int startOffset = 0)
            {
                var batch = new List<Record>();
                try
                {
                    var paginatedQuery = $"{_query} OFFSET {startOffset} LIMIT {batchSize}";
                    using var connection = new DuckDBConnection(_connectionString);
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
                    SimpleLogger.Error($"[DuckDbLoader] Error loading records in batch: {ex.Message}");
                }

                foreach (var record in batch)
                    yield return record;

                SimpleLogger.Info($"[DuckDbLoader] Yielded {batch.Count} records in batch.");
            }

            public async Task<List<Record>> LoadRecordsAsync()
            {
                return await Task.Run(() => LoadRecords());
            }

            public async IAsyncEnumerable<Record> LoadRecordsInBatchesAsync(int batchSize, int startOffset = 0)
            {
                foreach (var record in LoadRecordsInBatches(batchSize, startOffset))
                {
                    yield return record;
                    await Task.Yield();
                }
            }



            public class RavenDbLoader : IDatabaseLoader
            {
                private readonly string _url;
                private readonly string _database;
                private readonly string _collection;

                public RavenDbLoader(string url, string database, string collection)
                {
                    _url = url;
                    _database = database;
                    _collection = collection;
                }

                public List<Record> LoadRecords()
                {
                    var records = new List<Record>();
                    try
                    {
                        using var store = new DocumentStore { Urls = new[] { _url }, Database = _database };
                        store.Initialize();
                        using var session = store.OpenSession();
                        var documents = session.Advanced.RawQuery<dynamic>($"from {_collection}").ToList();

                        foreach (var doc in documents)
                        {
                            var dict = new Dictionary<string, string>();
                            foreach (var prop in doc)
                            {
                                string name = prop.Name;
                                string value = prop.Value?.ToString() ?? "";
                                if (name.ToLower() != "id")
                                    dict[name] = value;
                            }
                            string id = doc.Id?.ToString() ?? "-1";
                            records.Add(new Record(id, dict));
                        }

                        SimpleLogger.Info($"[RavenDbLoader] Loaded {records.Count} records.");
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.Error($"[RavenDbLoader] Error loading records: {ex.Message}");
                    }

                    return records;
                }

                public IEnumerable<Record> LoadRecordsInBatches(int batchSize, int startOffset = 0)
                {
                    try
                    {
                        var all = LoadRecords();
                        var batch = all.Skip(startOffset).Take(batchSize).ToList();
                        SimpleLogger.Info($"[RavenDbLoader] Yielded {batch.Count} records in batch.");
                        return batch;
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.Error($"[RavenDbLoader] Error loading records in batch: {ex.Message}");
                        return Enumerable.Empty<Record>();
                    }
                }

                public Task<List<Record>> LoadRecordsAsync() => Task.FromResult(LoadRecords());

                public async IAsyncEnumerable<Record> LoadRecordsInBatchesAsync(int batchSize, int startOffset = 0)
                {
                    foreach (var record in LoadRecordsInBatches(batchSize, startOffset))
                    {
                        yield return record;
                        await Task.Yield();
                    }
                }
            }


            public static class DatabaseLoaderFactory
            {
                public static IDatabaseLoader CreateLoader(string type, string connectionStringOrUrl, string queryOrCollection, string providerName = null)
                {
                    return type.ToLower() switch
                    {
                        "duckdb" => new DuckDbLoader(connectionStringOrUrl, queryOrCollection),
                        "ravendb" => new RavenDbLoader(connectionStringOrUrl, queryOrCollection, providerName),
                        "generic" => new GenericDbLoader(providerName, connectionStringOrUrl, queryOrCollection),
                        _ => throw new ArgumentException($"Unsupported database type: {type}")
                    };

                }
            }
        }
    }
}