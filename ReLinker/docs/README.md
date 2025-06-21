# ReLinker  
**A Powerful C# Record Linkage (Entity Resolution) Framework**

---

## 🚀 Overview

ReLinker is a flexible and efficient .NET library for deduplicating and linking records across disparate datasets. It supports advanced string similarity algorithms, blocking strategies, probabilistic matching, and clustering—all designed to scale for real-world data integration tasks.

---

## 📚 Key Concepts

- **Record Linkage**: Identifying records that refer to the same entity within or across databases.
- **Blocking**: Reducing the number of candidate record pairs for comparison.
- **Similarity Computation**: Quantifying how similar two records are.
- **Probabilistic Matching**: Scoring and classifying pairs using statistical models.
- **Clustering**: Grouping matched records into entities.

---

## 🏗️ System Architecture

The ReLinker pipeline consists of:
1. **Data Loading**
2. **Blocking**
3. **Similarity Computation**
4. **Probabilistic Scoring (Fellegi-Sunter + EM)**
5. **Clustering (Union-Find)**

---

## 🧩 Core Components

### 1. Record Structure

```csharp
public class Record
{
    public string Id { get; set; }
    public Dictionary<string, string> Fields { get; set; } = new();
}
```

---

### 2. Data Loading System

**Loader Interface:**
```csharp
public interface IDatabaseLoader
{
    Task<List<Record>> LoadRecordsAsync();
    IEnumerable<Record> LoadRecordsInBatches(int batchSize, int startOffset = 0);
}
```

**Factory:**
```csharp
public static class DatabaseLoaderFactory
{
    public static IDatabaseLoader CreateLoader(
        string type, string connectionStringOrUrl, string queryOrCollection, string providerName = null)
    {
        return type.ToLower() switch
        {
            "generic" => new GenericDbLoader(connectionStringOrUrl, queryOrCollection, providerName),
            "duckdb" => new DuckDbLoader(connectionStringOrUrl, queryOrCollection),
            "ravendb" => new RavenDbLoader(connectionStringOrUrl, queryOrCollection),
            _ => throw new ArgumentException("Unknown loader type.")
        };
    }
}
```

---

### 3. Blocking

**Blocking Rule:**
```csharp
public class BlockingRule
{
    public string Name { get; set; }
    public Func<Record, string> RuleFunc { get; set; }
}
```

**Blocking Helper:**
```csharp
public static class BlockingHelper
{
    public static IEnumerable<(Record, Record)> GenerateCandidatePairs(
        List<Record> records, List<BlockingRule> rules)
    {
        var blocks = new Dictionary<string, List<Record>>();
        foreach (var rule in rules)
        {
            foreach (var record in records)
            {
                var key = rule.RuleFunc(record);
                if (!blocks.ContainsKey(key))
                    blocks[key] = new List<Record>();
                blocks[key].Add(record);
            }
        }

        var seen = new HashSet<(string, string)>();
        foreach (var block in blocks.Values)
        for (int i = 0; i < block.Count; i++)
        for (int j = i + 1; j < block.Count; j++)
        {
            var a = block[i].Id;
            var b = block[j].Id;
            if (seen.Add((a, b)) && seen.Add((b, a)))
                yield return (block[i], block[j]);
        }
    }
}
```

---

### 4. Similarity Functions

Define similarity logic per field:
```csharp
public class SimilarityFunction
{
    public string FieldName { get; set; }
    public Func<Record, Record, double> Compute { get; set; }
}
```

**Example Implementations (see below for usage):**
- Levenshtein, Jaro, and TF-IDF similarity
- Field-specific (e.g., exact match for phone/email)

---

### 5. Probabilistic Matching (Fellegi-Sunter)

```csharp
public static class MatchScorer
{
    public static List<ScoredPair> Score(
        IEnumerable<(Record, Record)> pairs,
        List<SimilarityFunction> functions,
        double[] mProbs, double[] uProbs)
    {
        var results = new List<ScoredPair>();
        foreach (var (r1, r2) in pairs)
        {
            double score = 0;
            for (int i = 0; i < functions.Count; i++)
            {
                double sim = functions[i].Compute(r1, r2);
                score += Math.Log((sim * mProbs[i]) / (sim * uProbs[i] + 1e-6) + 1e-6);
            }
            results.Add(new ScoredPair { Record1 = r1, Record2 = r2, Score = score });
        }
        return results;
    }

    public static (double[], double[]) EstimateParametersWithEM(
        List<ScoredPair> scoredPairs, List<SimilarityFunction> functions, int maxIterations = 10)
    {
        // (see full implementation in repo)
        throw new NotImplementedException("EM implementation here...");
    }
}

public class ScoredPair
{
    public Record Record1 { get; set; }
    public Record Record2 { get; set; }
    public double Score { get; set; }
}
```

---

### 6. Clustering (Union-Find)

```csharp
public class UnionFind
{
    private readonly Dictionary<string, string> parent = new();
    public string Find(string x) => parent[x] == x ? x : parent[x] = Find(parent[x]);
    public void Union(string x, string y)
    {
        if (!parent.ContainsKey(x)) parent[x] = x;
        if (!parent.ContainsKey(y)) parent[y] = y;
        string rootX = Find(x), rootY = Find(y);
        if (rootX != rootY) parent[rootY] = rootX;
    }
    public Dictionary<string, List<string>> GetClusters()
    {
        var clusters = new Dictionary<string, List<string>>();
        foreach (var item in parent.Keys)
        {
            var root = Find(item);
            if (!clusters.ContainsKey(root)) clusters[root] = new();
            clusters[root].Add(item);
        }
        return clusters;
    }
}
```

---

## 🧑‍💻 Full Working Example: Customer Deduplication

```csharp
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

```

**Helper: Build IDF Dictionary**
```csharp
public static Dictionary<string, double> BuildIdfDictionary(List<Record> records)
{
    var termDocCounts = new Dictionary<string, int>();
    int totalDocs = records.Count;

    foreach (var record in records)
    {
        var allText = string.Join(" ", record.Fields.Values).ToLower();
        var terms = allText.Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).Distinct();
        foreach (var term in terms)
            termDocCounts[term] = termDocCounts.GetValueOrDefault(term, 0) + 1;
    }

    return termDocCounts.ToDictionary(
        kvp => kvp.Key,
        kvp => Math.Log((double)totalDocs / kvp.Value)
    );
}
```

---

## 🏁 Getting Started Checklist

1. **Set up your database and install dependencies.**
2. **Decide on blocking and similarity rules for your domain.**
3. **Load your records and build an IDF dictionary.**
4. **Run the pipeline as shown above.**
5. **Tune thresholds and inspect clusters.**

---

## ⚙️ Extending ReLinker

- Add new database loaders by implementing `IDatabaseLoader`.
- Add new similarity functions for domain-specific fields.
- Use advanced blocking (multiple rules, phonetic codes) for better recall.
- Integrate with logging/monitoring as needed.

---

## ⚠️ Limitations & Considerations

- No phonetic similarity (Soundex/Metaphone) out of the box—PRs welcome!
- Only string fields are currently supported for similarity.
- Memory usage increases with dataset size; blocking is essential.
- Data cleaning/standardization before linkage is strongly recommended.

---

## 📎 References

- [Fellegi-Sunter Model](https://en.wikipedia.org/wiki/Fellegi%E2%80%93Sunter_model_of_record_linkage)
- [TF-IDF Similarity](https://scikit-learn.org/stable/modules/feature_extraction.html#tfidf-term-weighting)
- [Jaro-Winkler Distance](https://en.wikipedia.org/wiki/Jaro%E2%80%93Winkler_distance)

---

**For more examples, advanced configuration, or to contribute, see the [GitHub repo](https://github.com/ModMaker101/ReLinker).**


