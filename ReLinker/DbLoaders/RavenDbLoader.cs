using Raven.Client.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReLinker
{
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
                Logger.Info($"[RavenDbLoader] Loaded {records.Count} records.");
            }
            catch (Exception ex)
            {
                Logger.Error($"[RavenDbLoader] Error loading records: {ex.Message}");
            }
            return records;
        }

        public IEnumerable<Record> LoadRecordsInBatches(int batchSize, int startOffset = 0)
        {
            try
            {
                var all = LoadRecords();
                var batch = all.Skip(startOffset).Take(batchSize).ToList();
                Logger.Info($"[RavenDbLoader] Yielded {batch.Count} records in batch.");
                return batch;
            }
            catch (Exception ex)
            {
                Logger.Error($"[RavenDbLoader] Error loading records in batch: {ex.Message}");
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
}
