# ReLinker

**ReLinker** is a high-performance, modular record linkage engine for deduplicating and clustering similar records across datasets. It combines probabilistic scoring, customizable similarity functions, and parallel processing to deliver fast and accurate entity resolution.

---

## Features

- **Multi-Field Similarity Matching**  
  Supports Levenshtein, Jaro, and TF-IDF similarity functions with IDF weighting.

- **Probabilistic Scoring Engine**  
  Computes log-likelihood ratios to assess match confidence.

- **EM-Based Parameter Estimation**  
  Automatically tunes match (m) and non-match (u) probabilities using Expectation-Maximization.

- **Parallelized for Performance**  
  Uses `Parallel.ForEach` to scale across large datasets.

- **Union-Find Clustering**  
  Efficiently groups matched records into disjoint clusters.

---

## Installation

### Using .NET CLI

```bash
dotnet add package ReLinker
```

### Using NuGet Package Manager

```powershell
NuGet\Install-Package ReLinker
```

---

## Example Usage

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using ReLinker;

class Program
{
    static void Main()
    {
        // Sample records
        var records = new List<Record>
        {
            new Record("1", new Dictionary<string, string> { { "name", "Alice Smith" }, { "city", "New York" } }),
            new Record("2", new Dictionary<string, string> { { "name", "Alicia Smyth" }, { "city", "New York" } }),
            new Record("3", new Dictionary<string, string> { { "name", "Bob Johnson" }, { "city", "Los Angeles" } })
        };

        // Define blocking rules (e.g., block on city)
        var blockingRules = BlockingHelper.LoadBlockingRulesFromConfig(new List<string> { "city" });

        // Generate candidate pairs
        var candidatePairs = BlockingHelper.GenerateCandidatePairsInBatches(records, blockingRules, batchSize: 2).ToList();

        // Define similarity function (e.g., Jaro on name)
        var idf = new Dictionary<string, double>(); // empty for now
        var similarityFunc = new SimilarityFunction
        {
            FieldName = "name",
            Compute = Similarity.SimilarityFactory.Create("jaro", "name", idf)
        };

        // Score pairs
        var scored = MatchScorer.Score(candidatePairs, new List<SimilarityFunction> { similarityFunc }, new[] { 0.9 }, new[] { 0.1 });

        // Output results
        foreach (var pair in scored)
        {
            Console.WriteLine($"{pair.Record1.Id} - {pair.Record2.Id}: Score = {pair.Score:F4}");
        }
    }
}
```

---

## Documentation

For full usage details, configuration options, and API reference, see the [Documentation README](./ReLinker/docs/README.md).

---

## Built With

- **C# / .NET**
- **Parallel LINQ**
- **Custom Similarity Functions**
- **EM Algorithm** for unsupervised parameter tuning
- **RavenDB** for Raven Support
- **DuckDB** for DuckDB support
---

## License

This project is licensed under the MIT License. See the [LICENSE](./LICENSE.txt) file for details.
