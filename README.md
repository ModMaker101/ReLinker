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
using ReLinker;
using ReLinker.Similarity;
using System.Collections.Generic;
using System.Linq;

// Define records
var record1 = new Record("1", new Dictionary<string, string> { ["name"] = "Alice Smith" });
var record2 = new Record("2", new Dictionary<string, string> { ["name"] = "Alicia Smythe" });

// Define similarity functions
var functions = new List<SimilarityFunction> { new LevenshteinFunction("name") };

// Define EM-learned m/u probabilities or use estimated values
double[] mProbs = { 0.9 };
double[] uProbs = { 0.1 };

// Score record pairs
var scored = MatchScorer.Score(
    new[] { (record1, record2) },
    functions,
    mProbs,
    uProbs
);

// Threshold for match acceptance
double threshold = 3.0;

// Cluster matched records
var uf = new UnionFind();
foreach (var pair in scored.Where(p => p.Score > threshold))
    uf.Union(pair.Record1.Id, pair.Record2.Id);

// Retrieve clusters (optional)
var clusters = uf.GetClusters(); // Dictionary<string, List<string>>
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

This project is licensed under the MIT License. See the [LICENSE](./LICENSE) file for details.
