using DuckDB.NET.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReLinker
{
    public class DuckDbLoader : IDatabaseLoader
    {
        private readonly string _connectionString;
        private readonly string _query;
        private readonly ILogger<DuckDbLoader> _logger;

        public DuckDbLoader(string connectionString, string query, ILogger<DuckDbLoader> logger)
        {
            _connectionString = connectionString;
            _query = query;
            _logger = logger;
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
                _logger.LogInformation("[DuckDbLoader] Loaded {Count} records.", records.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DuckDbLoader] Error loading records: {Message}", ex.Message);
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
                _logger.LogError(ex, "[DuckDbLoader] Error loading records in batch: {Message}", ex.Message);
            }
            foreach (var record in batch)
                yield return record;
            _logger.LogInformation("[DuckDbLoader] Yielded {Count} records in batch.", batch.Count);
        }

        public async Task<List<Record>> LoadRecordsAsync() => await Task.Run(() => LoadRecords());

        public async IAsyncEnumerable<Record> LoadRecordsInBatchesAsync(int batchSize, int startOffset = 0)
        {
            foreach (var record in LoadRecordsInBatches(batchSize, startOffset))
            {
                yield return record;
                await Task.Yield();
            }
        }
    }
}
