using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReLinker;

class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();


        services.AddLogging(configure => configure.AddConsole());


        services.AddSingleton<Similarity>();
        services.AddSingleton<LevenshteinSimularity>();
        services.AddSingleton<JaroSimularity>();
        services.AddSingleton<TfIdfSimularity>();
        services.AddSingleton<SimilarityFactory>(provider =>
        {
            var factory = new SimilarityFactory();
            factory.Register<LevenshteinSimularity>(provider.GetRequiredService< LevenshteinSimularity>());
            factory.Register<JaroSimularity>(provider.GetRequiredService<JaroSimularity>());
            factory.Register<TfIdfSimularity>(provider.GetRequiredService<TfIdfSimularity>());
            return factory;
        });

        services.AddSingleton<IDatabaseLoader, InMemoryLoader>();
        services.AddSingleton<MatchScorer>();
        services.AddSingleton<BlockingHelper>();
        services.AddSingleton<DisjointSetForest>();
        services.AddSingleton<IReLinker, ReLinkerEngine>();

        var serviceProvider = services.BuildServiceProvider();
        var relinker = serviceProvider.GetRequiredService<IReLinker>();


        var idf = new Dictionary<string, double>();

        var factory = serviceProvider.GetRequiredService<SimilarityFactory>();
        var simFuncs = new List<SimilarityFunction>
        {
            new SimilarityFunction
            {
                FieldName = "name",
                Compute = factory.Create<LevenshteinSimularity>("name", idf)
            },
            new SimilarityFunction
            {
                FieldName = "address",
                Compute = factory.Create<JaroSimularity>("address", idf)
            }
        };


        var options = new ReLinkerOptions
        {
            BlockingFields = new List<string> { "name", "adress" },
            SimilarityFunctions = simFuncs,
            MProbs = new double[] { 0.9, 0.8 },
            UProbs = new double[] { 0.1, 0.2 },
            MatchThreshold = 0.5,
            BatchSize = 100
        };


        relinker.ValidateOptions(options);

        var (mProbs, uProbs) = await relinker.EstimateParametersAsync(options);
        Console.WriteLine("Estimated mProbs: " + string.Join(", ", mProbs));
        Console.WriteLine("Estimated uProbs: " + string.Join(", ", uProbs));


        var scored = await relinker.ScoreCandidatePairsAsync(options);
        Console.WriteLine($"Scored {scored.Count} pairs.");


        var clusters = await relinker.LinkRecordsWithDetailsAsync(options);
        Console.WriteLine($"Found {clusters.Count} clusters:");
        foreach (var cluster in clusters)
        {
            Console.WriteLine("Cluster:");
            foreach (var record in cluster)
            {
                Console.WriteLine($"  - {record.Id}: {string.Join(", ", record.Fields)}");
            }
        }
    }
}


public class InMemoryLoader : IDatabaseLoader
{
    public List<Record> LoadRecords()
    {
        return new List<Record>
        {
            new Record("1", new Dictionary<string, string> { { "name", "Alice" }, { "address", "123 Main St" } }),
            new Record("2", new Dictionary<string, string> { { "name", "Alicia" }, { "address", "123 Main Street" } }),
            new Record("3", new Dictionary<string, string> { { "name", "Bob" }, { "address", "456 Elm St" } }),
            new Record("4", new Dictionary<string, string> { { "name", "Bobb" }, { "address", "456 Elm St" } }),
        };
    }

    public Task<List<Record>> LoadRecordsAsync() => Task.FromResult(LoadRecords());

    public IAsyncEnumerable<Record> LoadRecordsInBatchesAsync(int batchSize, int startOffset = 0)
        => throw new NotImplementedException();

    public IEnumerable<Record> LoadRecordsInBatches(int batchSize, int startOffset = 0)
        => throw new NotImplementedException();
}
