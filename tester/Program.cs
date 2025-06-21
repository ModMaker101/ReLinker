using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ReLinker;

class Program
{
    static async Task Main()
    {
        // 1. Load records from a PostgreSQL database
        var loader = DatabaseLoaderFactory.CreateGenericLoader(
            "Npgsql", // PostgreSQL provider
            "Host=localhost;Port=5432;Username=your_user;Password=your_password;Database=your_db", // Update with your credentials
            "SELECT id, first_name, last_name, phone, email, address FROM customers"
        );

        var records = await loader.LoadRecordsAsync();

        // 2. Build IDF dictionary
        var idf = BuildIdfDictionary(records);

        // 3. Define blocking rules
        var blockingRules = new List<BlockingRule>
        {
            new BlockingRule("PhonePrefix", r => r.Fields["phone"][..6]),
            new BlockingRule("EmailDomain", r => r.Fields["email"].Split('@')[1])
        };

        // 4. Define similarity functions
        var similarities = new List<SimilarityFunction>
        {
            new SimilarityFunction
            {
                FieldName = "FullName",
                Compute = (r1, r2) =>
                {
                    string name1 = $"{r1.Fields["first_name"]} {r1.Fields["last_name"]}";
                    string name2 = $"{r2.Fields["first_name"]} {r2.Fields["last_name"]}";
                    return Similarity.JaroSimilarity(name1, name2, idf);
                }
            },
            new SimilarityFunction
            {
                FieldName = "Address",
                Compute = (r1, r2) => Similarity.LevenshteinSimilarity(r1.Fields["address"], r2.Fields["address"], idf)
            },
            new SimilarityFunction
            {
                FieldName = "Phone",
                Compute = (r1, r2) => r1.Fields["phone"] == r2.Fields["phone"] ? 1.0 : 0.0
            }
        };

        // 5. Generate candidate pairs
        var pairs = BlockingHelper.GenerateCandidatePairsInBatches(records, blockingRules, 1000).ToList();

        // 6. Score pairs and estimate parameters
        double[] mProbs = { 0.9, 0.8, 0.95 }, uProbs = { 0.1, 0.1, 0.05 };
        var initialScores = MatchScorer.Score(pairs, similarities, mProbs, uProbs);

        // 7. Cluster matches
        double threshold = 1.0;
        var unionFind = new DisjointSetForest();
        foreach (var pair in initialScores.Where(p => p.Score > threshold))
            unionFind.MergeEntities(pair.Record1.Id, pair.Record2.Id);

        var clusters = unionFind.GetClusters();
        foreach (var cluster in clusters.Values.Where(c => c.Count > 1))
            Console.WriteLine($"Duplicate group: {string.Join(", ", cluster)}");
    }

    static Dictionary<string, double> BuildIdfDictionary(List<Record> records)
    {
        var tokenCounts = new Dictionary<string, int>();
        int totalDocs = records.Count;

        foreach (var record in records)
        {
            var tokens = new HashSet<string>(
                record.Fields.Values.SelectMany(v => v.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            );
            foreach (var token in tokens)
            {
                if (!tokenCounts.ContainsKey(token))
                    tokenCounts[token] = 0;
                tokenCounts[token]++;
            }
        }

        return tokenCounts.ToDictionary(
            kvp => kvp.Key,
            kvp => Math.Log((double)totalDocs / (1 + kvp.Value))
        );
    }
}
