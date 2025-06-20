# ReLinker: Record Linkage System Documentation

## Overview

ReLinker is a comprehensive C# record linkage (entity resolution) system designed to identify and match duplicate or related records across datasets. The system implements sophisticated algorithms for blocking, similarity computation, probabilistic matching, and clustering to efficiently process large datasets and find potential matches.

## Key Concepts

### Record Linkage
Record linkage is the process of identifying records that refer to the same entity across different data sources or within the same dataset. This is crucial for:
- Data deduplication
- Master data management
- Entity resolution
- Data integration

### System Architecture
The ReLinker system follows a multi-stage pipeline:
1. **Data Loading** - Extract records from various database sources
2. **Blocking** - Reduce comparison space by grouping similar records
3. **Similarity Computation** - Calculate similarity scores between record pairs
4. **Probabilistic Matching** - Use statistical models to determine match likelihood
5. **Clustering** - Group matched records into entities

## Core Components

### 1. Record Structure

```csharp
public class Record
{
    public string Id { get; set; }
    public Dictionary<string, string> Fields { get; set; }
}
```

The fundamental data structure representing a single record with:
- **Id**: Unique identifier for the record
- **Fields**: Key-value pairs containing the record's attributes

### 2. Data Loading System

#### Interface: IDatabaseLoader
Provides a unified interface for loading records from different database systems:

```csharp
public interface IDatabaseLoader
{
    Task<List<Record>> LoadRecordsAsync();
    IAsyncEnumerable<Record> LoadRecordsInBatchesAsync(int batchSize, int startOffset = 0);
    List<Record> LoadRecords();
    IEnumerable<Record> LoadRecordsInBatches(int batchSize, int startOffset = 0);
}
```

#### Supported Database Types

**1. GenericDbLoader**
- Supports any ADO.NET-compatible database
- Uses DbProviderFactories for database abstraction
- Parameters: provider name, connection string, SQL query

**2. DuckDbLoader**
- Specialized loader for DuckDB (analytical database)
- Optimized for columnar data processing
- Parameters: connection string, SQL query

**3. RavenDbLoader**
- Document database loader for RavenDB
- Handles JSON document structures
- Parameters: URL, database name, collection name

#### Factory Pattern
```csharp
public static IDatabaseLoader CreateLoader(string type, string connectionStringOrUrl, 
    string queryOrCollection, string providerName = null)
```

Creates appropriate loader based on database type specification.

### 3. Blocking System

Blocking is an optimization technique that reduces the number of record comparisons by only comparing records that share certain characteristics.

#### BlockingRule
```csharp
public class BlockingRule
{
    public string Name { get; set; }
    public Func<Record, string> RuleFunc { get; set; }
}
```

Defines a blocking strategy:
- **Name**: Descriptive name for the rule
- **RuleFunc**: Function that extracts a blocking key from a record

#### BlockingHelper
Generates candidate pairs using blocking rules:
- Processes records in batches for memory efficiency
- Uses parallel processing for performance
- Only compares records with matching blocking keys

**Example Blocking Rules:**
- First three characters of last name
- Soundex code of name
- ZIP code prefix
- Date of birth year

### 4. Similarity Functions

The system implements multiple string similarity algorithms:

#### Levenshtein Similarity (Token-based with IDF weighting)
- Computes edit distance between tokenized strings
- Weights operations by Inverse Document Frequency (IDF)
- Better handles importance of rare vs. common terms

#### Jaro Similarity (IDF-weighted)
- Focuses on character transpositions and matches
- Weighted by term importance using IDF
- Good for names with common misspellings

#### TF-IDF Cosine Similarity
- Treats strings as document vectors
- Uses Term Frequency-Inverse Document Frequency weighting
- Effective for longer text fields

#### SimilarityFunction Structure
```csharp
public class SimilarityFunction
{
    public string FieldName { get; set; }
    public Func<Record, Record, double> Compute { get; set; }
}
```

### 5. Probabilistic Matching

#### Fellegi-Sunter Model
The system implements the classic Fellegi-Sunter probabilistic record linkage model:

- **M-probability**: Probability that a field agrees given records are a true match
- **U-probability**: Probability that a field agrees given records are not a match
- **Log-likelihood ratio**: Combines evidence from multiple fields

#### MatchScorer
```csharp
public static List<ScoredPair> Score(
    IEnumerable<(Record, Record)> pairs,
    List<SimilarityFunction> functions,
    double[] mProbs,
    double[] uProbs)
```

Computes match scores using the Fellegi-Sunter model.

#### Expectation-Maximization (EM) Algorithm
```csharp
public static (double[], double[]) EstimateParametersWithEM(
    List<ScoredPair> scoredPairs,
    List<SimilarityFunction> functions,
    int maxIterations = 10)
```

Automatically estimates M and U probabilities from unlabeled data:
1. **E-step**: Estimate match probabilities for each pair
2. **M-step**: Update M and U parameters based on estimated probabilities
3. **Iterate**: Repeat until convergence

### 6. Clustering System

#### UnionFind Data Structure
Implements efficient clustering using Union-Find (Disjoint Set Union):

```csharp
public class UnionFind
{
    public string Find(string x)      // Find root of component
    public void Union(string x, string y)  // Merge two components
    public Dictionary<string, List<string>> GetClusters()  // Get final clusters
}
```

**Features:**
- Path compression optimization
- Efficient O(α(n)) amortized time complexity
- Groups records that should be considered the same entity

## System Workflow

### 1. Data Preparation
```csharp
// Load records from database
var loader = DatabaseLoaderFactory.CreateLoader("duckdb", connectionString, query);
var records = await loader.LoadRecordsAsync();
```

### 2. Define Blocking Rules
```csharp
var blockingRules = new List<BlockingRule>
{
    new BlockingRule("LastName3", r => r.Fields["LastName"].Substring(0, 3)),
    new BlockingRule("ZipCode", r => r.Fields["ZipCode"])
};
```

### 3. Generate Candidate Pairs
```csharp
var candidatePairs = BlockingHelper.GenerateCandidatePairsInBatches(
    records, blockingRules, batchSize: 1000);
```

### 4. Define Similarity Functions
```csharp
var similarities = new List<SimilarityFunction>
{
    new SimilarityFunction 
    { 
        FieldName = "Name", 
        Compute = (r1, r2) => Similarity.JaroSimilarity(
            r1.Fields["Name"], r2.Fields["Name"], idfDictionary)
    }
};
```

### 5. Score Pairs and Estimate Parameters
```csharp
// Initial scoring with default parameters
var initialScores = MatchScorer.Score(candidatePairs, similarities, mProbs, uProbs);

// Refine parameters using EM
var (refinedM, refinedU) = MatchScorer.EstimateParametersWithEM(
    initialScores, similarities, maxIterations: 10);

// Final scoring with refined parameters
var finalScores = MatchScorer.Score(candidatePairs, similarities, refinedM, refinedU);
```

### 6. Apply Threshold and Cluster
```csharp
var unionFind = new UnionFind();
foreach (var pair in finalScores.Where(p => p.Score > threshold))
{
    unionFind.Union(pair.Record1.Id, pair.Record2.Id);
}
var clusters = unionFind.GetClusters();
```

## Advanced Features

### Batch Processing
The system supports both synchronous and asynchronous batch processing:
- Memory-efficient streaming for large datasets
- Configurable batch sizes
- Offset-based pagination support

### Parallel Processing
- Multi-threaded candidate pair generation
- Parallel similarity computation
- Thread-safe operations with proper locking

### Retry Mechanism
```csharp
public static T RetryWithBackoff<T>(Func<T> operation, 
    int maxRetries = 3, int delayMilliseconds = 500)
```
Implements exponential backoff for database operations.

### Logging
Simple logging system for monitoring:
- Info-level logging for successful operations
- Error-level logging for exceptions
- Timestamped log entries

## Performance Considerations

### Blocking Effectiveness
- Good blocking rules can reduce comparisons from O(n²) to O(n·k)
- Multiple blocking rules can improve recall
- Balance between precision and recall

### Memory Management
- Batch processing prevents memory overflow
- Streaming interfaces for large datasets
- Efficient data structures (UnionFind)

### Computational Complexity
- Similarity computation: O(n·m) per pair (n,m = string lengths)
- EM algorithm: O(iterations · pairs · features)
- UnionFind clustering: O(n·α(n)) amortized

## Configuration Examples

### Example 1: Customer Deduplication
```csharp
// Step 1: Set up database loader
var connectionString = "Data Source=customers.db";
var query = "SELECT id, first_name, last_name, phone, email, address FROM customers";
var loader = DatabaseLoaderFactory.CreateLoader("generic", connectionString, query, "System.Data.SQLite");

// Step 2: Load records
var records = await loader.LoadRecordsAsync();

// Step 3: Build IDF dictionary for weighting (you'd calculate this from your corpus)
var idf = new Dictionary<string, double>
{
    {"john", 0.5}, {"smith", 0.3}, {"the", 0.1}, {"inc", 0.7}, 
    {"street", 0.2}, {"avenue", 0.2} // Add more terms as needed
};

// Step 4: Define blocking rules to reduce comparisons
var blockingRules = new List<BlockingRule>
{
    // Compare records with same first 6 digits of phone
    new BlockingRule("Phone", r => 
    {
        var phone = r.Fields.GetValueOrDefault("phone", "");
        return phone.Length >= 6 ? phone.Substring(0, 6) : phone;
    }),
    // Compare records with same email domain
    new BlockingRule("Email", r => 
    {
        var email = r.Fields.GetValueOrDefault("email", "");
        var parts = email.Split('@');
        return parts.Length > 1 ? parts[1] : "";
    })
};

// Step 5: Define similarity functions for each field
var similarities = new List<SimilarityFunction>
{
    new SimilarityFunction 
    { 
        FieldName = "FullName",
        Compute = (r1, r2) => 
        {
            var name1 = $"{r1.Fields.GetValueOrDefault("first_name", "")} {r1.Fields.GetValueOrDefault("last_name", "")}";
            var name2 = $"{r2.Fields.GetValueOrDefault("first_name", "")} {r2.Fields.GetValueOrDefault("last_name", "")}";
            return Similarity.JaroSimilarity(name1, name2, idf);
        }
    },
    new SimilarityFunction 
    { 
        FieldName = "Address",
        Compute = (r1, r2) => Similarity.LevenshteinSimilarity(
            r1.Fields.GetValueOrDefault("address", ""), 
            r2.Fields.GetValueOrDefault("address", ""), idf)
    },
    new SimilarityFunction 
    { 
        FieldName = "Phone",
        Compute = (r1, r2) => 
        {
            var phone1 = r1.Fields.GetValueOrDefault("phone", "");
            var phone2 = r2.Fields.GetValueOrDefault("phone", "");
            return phone1 == phone2 ? 1.0 : 0.0; // Exact match for phones
        }
    }
};

// Step 6: Generate candidate pairs using blocking
var candidatePairs = BlockingHelper.GenerateCandidatePairsInBatches(
    records, blockingRules, batchSize: 1000);

// Step 7: Initial parameters (will be refined by EM)
double[] initialM = {0.9, 0.8, 0.95}; // High probability of agreement for matches
double[] initialU = {0.1, 0.1, 0.05}; // Low probability of agreement for non-matches

// Step 8: Score pairs and refine parameters
var initialScores = MatchScorer.Score(candidatePairs, similarities, initialM, initialU);
var (refinedM, refinedU) = MatchScorer.EstimateParametersWithEM(initialScores, similarities, 10);
var finalScores = MatchScorer.Score(candidatePairs, similarities, refinedM, refinedU);

// Step 9: Apply threshold and cluster matches
double threshold = 2.0; // Log-likelihood ratio threshold (tune based on your needs)
var unionFind = new UnionFind();
foreach (var pair in finalScores.Where(p => p.Score > threshold))
{
    unionFind.Union(pair.Record1.Id, pair.Record2.Id);
}
var clusters = unionFind.GetClusters();

// Step 10: Process results
foreach (var cluster in clusters.Where(c => c.Value.Count > 1))
{
    Console.WriteLine($"Found duplicate group: {string.Join(", ", cluster.Value)}");
}
```

### Example 2: Product Matching Across Catalogs
```csharp
// Step 1: Load products from two different sources
var loader1 = DatabaseLoaderFactory.CreateLoader("duckdb", 
    "Data Source=:memory:", 
    "SELECT id, title, brand, category, price FROM catalog1");
var loader2 = DatabaseLoaderFactory.CreateLoader("duckdb", 
    "Data Source=:memory:", 
    "SELECT id, product_name, manufacturer, type, cost FROM catalog2");

var catalog1 = await loader1.LoadRecordsAsync();
var catalog2 = await loader2.LoadRecordsAsync();
var allProducts = catalog1.Concat(catalog2).ToList();

// Step 2: Build IDF dictionary for product terms
var idf = new Dictionary<string, double>
{
    {"laptop", 0.8}, {"computer", 0.7}, {"apple", 0.9}, {"samsung", 0.9},
    {"black", 0.2}, {"white", 0.2}, {"pro", 0.4}, {"plus", 0.3},
    {"inch", 0.1}, {"gb", 0.1} // Add domain-specific terms
};

// Step 3: Blocking rules for products
var blockingRules = new List<BlockingRule>
{
    // Group by brand/manufacturer
    new BlockingRule("Brand", r => 
    {
        var brand = r.Fields.GetValueOrDefault("brand", "") + 
                   r.Fields.GetValueOrDefault("manufacturer", "");
        return brand.ToLower().Trim();
    }),
    // Group by category/type
    new BlockingRule("Category", r => 
    {
        var category = r.Fields.GetValueOrDefault("category", "") + 
                      r.Fields.GetValueOrDefault("type", "");
        return category.ToLower().Trim();
    }),
    // Group by price range (within $50)
    new BlockingRule("PriceRange", r => 
    {
        var priceStr = r.Fields.GetValueOrDefault("price", "") + 
                      r.Fields.GetValueOrDefault("cost", "");
        if (double.TryParse(priceStr, out double price))
        {
            return ((int)(price / 50) * 50).ToString(); // Round to nearest $50
        }
        return "unknown";
    })
};

// Step 4: Product similarity functions
var similarities = new List<SimilarityFunction>
{
    new SimilarityFunction 
    { 
        FieldName = "ProductTitle",
        Compute = (r1, r2) => 
        {
            var title1 = r1.Fields.GetValueOrDefault("title", "") + 
                         r1.Fields.GetValueOrDefault("product_name", "");
            var title2 = r2.Fields.GetValueOrDefault("title", "") + 
                         r2.Fields.GetValueOrDefault("product_name", "");
            return Similarity.TfIdfSimilarity(title1, title2, idf);
        }
    },
    new SimilarityFunction 
    { 
        FieldName = "Brand",
        Compute = (r1, r2) => 
        {
            var brand1 = (r1.Fields.GetValueOrDefault("brand", "") + 
                         r1.Fields.GetValueOrDefault("manufacturer", "")).ToLower();
            var brand2 = (r2.Fields.GetValueOrDefault("brand", "") + 
                         r2.Fields.GetValueOrDefault("manufacturer", "")).ToLower();
            return brand1 == brand2 ? 1.0 : 0.0;
        }
    },
    new SimilarityFunction 
    { 
        FieldName = "Price",
        Compute = (r1, r2) => 
        {
            var price1Str = r1.Fields.GetValueOrDefault("price", "") + 
                           r1.Fields.GetValueOrDefault("cost", "");
            var price2Str = r2.Fields.GetValueOrDefault("price", "") + 
                           r2.Fields.GetValueOrDefault("cost", "");
            
            if (double.TryParse(price1Str, out double price1) && 
                double.TryParse(price2Str, out double price2))
            {
                double priceDiff = Math.Abs(price1 - price2);
                double avgPrice = (price1 + price2) / 2;
                return Math.Max(0, 1 - (priceDiff / avgPrice)); // Similarity decreases with price difference
            }
            return 0.0;
        }
    }
};

// Continue with scoring and clustering as in Example 1...
```

### Example 3: Complete End-to-End Pipeline
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ReLinker;

public class RecordLinkagePipeline
{
    public async Task<Dictionary<string, List<string>>> RunPipeline(
        string dbType, 
        string connectionString, 
        string query,
        Dictionary<string, double> idfDictionary,
        double threshold = 2.0)
    {
        try
        {
            // Step 1: Load data
            SimpleLogger.Info("Loading records from database...");
            var loader = DatabaseLoaderFactory.CreateLoader(dbType, connectionString, query);
            var records = await loader.LoadRecordsAsync();
            SimpleLogger.Info($"Loaded {records.Count} records");

            // Step 2: Define blocking and similarity based on your data structure
            var (blockingRules, similarities) = ConfigureForYourDomain(idfDictionary);

            // Step 3: Generate pairs
            SimpleLogger.Info("Generating candidate pairs...");
            var candidatePairs = BlockingHelper.GenerateCandidatePairsInBatches(
                records, blockingRules, batchSize: 5000).ToList();
            SimpleLogger.Info($"Generated {candidatePairs.Count} candidate pairs");

            // Step 4: Score and cluster
            SimpleLogger.Info("Scoring pairs...");
            double[] mProbs = Enumerable.Repeat(0.9, similarities.Count).ToArray();
            double[] uProbs = Enumerable.Repeat(0.1, similarities.Count).ToArray();

            var scores = MatchScorer.Score(candidatePairs, similarities, mProbs, uProbs);
            
            SimpleLogger.Info("Refining parameters with EM...");
            var (refinedM, refinedU) = MatchScorer.EstimateParametersWithEM(scores, similarities);
            
            var finalScores = MatchScorer.Score(candidatePairs, similarities, refinedM, refinedU);

            // Step 5: Cluster
            SimpleLogger.Info("Clustering matches...");
            var unionFind = new UnionFind();
            var matchCount = 0;
            
            foreach (var pair in finalScores.Where(p => p.Score > threshold))
            {
                unionFind.Union(pair.Record1.Id, pair.Record2.Id);
                matchCount++;
            }
            
            var clusters = unionFind.GetClusters();
            var duplicateGroups = clusters.Where(c => c.Value.Count > 1).Count();
            
            SimpleLogger.Info($"Found {matchCount} matches forming {duplicateGroups} duplicate groups");
            
            return clusters;
        }
        catch (Exception ex)
        {
            SimpleLogger.Error($"Pipeline failed: {ex.Message}");
            throw;
        }
    }

    private (List<BlockingRule>, List<SimilarityFunction>) ConfigureForYourDomain(
        Dictionary<string, double> idf)
    {
        // Customize these based on your specific data fields!
        var blocking = new List<BlockingRule>
        {
            new BlockingRule("FirstChar", r => 
            {
                var name = r.Fields.GetValueOrDefault("name", "") + 
                          r.Fields.GetValueOrDefault("company_name", "");
                return string.IsNullOrEmpty(name) ? "" : name.Substring(0, 1).ToUpper();
            })
        };

        var similarities = new List<SimilarityFunction>
        {
            new SimilarityFunction 
            { 
                FieldName = "Name",
                Compute = (r1, r2) => 
                {
                    var name1 = r1.Fields.GetValueOrDefault("name", "") + 
                               r1.Fields.GetValueOrDefault("company_name", "");
                    var name2 = r2.Fields.GetValueOrDefault("name", "") + 
                               r2.Fields.GetValueOrDefault("company_name", "");
                    return Similarity.JaroSimilarity(name1, name2, idf);
                }
            }
        };

        return (blocking, similarities);
    }
}

// Usage:
var pipeline = new RecordLinkagePipeline();
var idf = BuildIdfDictionary(); // You need to implement this based on your data
var results = await pipeline.RunPipeline("duckdb", "Data Source=mydata.db", 
    "SELECT * FROM records", idf, threshold: 1.5);
```

## Getting Started Guide

### Quick Start Checklist

Before you begin, you'll need:

1. **Your database connection details**:
   - Connection string for your database
   - SQL query to retrieve records (must have an ID column as first column)
   - Provider name for generic databases (e.g., "System.Data.SqlClient", "System.Data.SQLite")

2. **Understanding of your data**:
   - What fields contain the information you want to match on?
   - What are the actual column names in your database?
   - What kind of data quality issues do you expect?

3. **Basic parameters to tune**:
   - **Batch size**: Start with 1000-5000 records per batch
   - **Match threshold**: Start with 1.5-2.0, adjust based on results
   - **EM iterations**: 10 is usually sufficient

### Step-by-Step Setup

#### Step 1: Prepare Your IDF Dictionary
The IDF (Inverse Document Frequency) dictionary weights the importance of terms. Here's how to build one:

```csharp
public Dictionary<string, double> BuildIdfDictionary(List<Record> records)
{
    var termDocumentCounts = new Dictionary<string, int>();
    var totalDocuments = records.Count;

    // Count how many records contain each term
    foreach (var record in records)
    {
        var allText = string.Join(" ", record.Fields.Values).ToLower();
        var terms = allText.Split(new[] { ' ', ',', '.', '!', '?' }, 
                                 StringSplitOptions.RemoveEmptyEntries);
        var uniqueTerms = new HashSet<string>(terms);
        
        foreach (var term in uniqueTerms)
        {
            termDocumentCounts[term] = termDocumentCounts.GetValueOrDefault(term, 0) + 1;
        }
    }

    // Calculate IDF: log(total_docs / docs_containing_term)
    var idf = new Dictionary<string, double>();
    foreach (var kvp in termDocumentCounts)
    {
        idf[kvp.Key] = Math.Log((double)totalDocuments / kvp.Value);
    }

    return idf;
}
```

#### Step 2: Choose Your Database Loader Parameters

**For SQL Server:**
```csharp
var loader = DatabaseLoaderFactory.CreateLoader(
    "generic",                                    // Type
    "Server=localhost;Database=MyDB;Trusted_Connection=true;", // Connection
    "SELECT id, first_name, last_name, email FROM customers",  // Query
    "System.Data.SqlClient"                      // Provider
);
```

**For SQLite:**
```csharp
var loader = DatabaseLoaderFactory.CreateLoader(
    "generic",
    "Data Source=C:\\path\\to\\database.db",
    "SELECT rowid, name, address, phone FROM contacts",
    "System.Data.SQLite"
);
```

**For DuckDB:**
```csharp
var loader = DatabaseLoaderFactory.CreateLoader(
    "duckdb",
    "Data Source=:memory:",  // or path to .duckdb file
    "SELECT id, product_name, brand FROM products",
    null  // No provider needed for DuckDB
);
```

#### Step 3: Understand Blocking Strategy

Blocking is crucial for performance. Choose blocking keys that:
- Group likely matches together
- Distribute records somewhat evenly
- Are not too restrictive (don't miss true matches)

**Common blocking strategies:**
```csharp
// For names - first few characters
new BlockingRule("NamePrefix", r => 
{
    var name = r.Fields.GetValueOrDefault("name", "").ToLower().Trim();
    return name.Length >= 3 ? name.Substring(0, 3) : name;
})

// For addresses - ZIP code
new BlockingRule("ZipCode", r => 
{
    var zip = r.Fields.GetValueOrDefault("zip_code", "");
    return zip.Length >= 5 ? zip.Substring(0, 5) : zip;
})

// For emails - domain
new BlockingRule("EmailDomain", r => 
{
    var email = r.Fields.GetValueOrDefault("email", "");
    var parts = email.Split('@');
    return parts.Length > 1 ? parts[1].ToLower() : "";
})

// For phone numbers - area code + first 3 digits
new BlockingRule("PhonePrefix", r => 
{
    var phone = r.Fields.GetValueOrDefault("phone", "").Replace("-", "").Replace("(", "").Replace(")", "");
    return phone.Length >= 6 ? phone.Substring(0, 6) : phone;
})
```

### 1. Blocking Strategy
- Use multiple complementary blocking rules
- Choose blocking keys with good discriminative power
- Monitor blocking effectiveness (pairs generated vs. true matches)

### 2. Similarity Functions
- Select appropriate similarity functions for each field type
- Use IDF weighting for better term importance handling
- Normalize similarity scores to [0,1] range

### 3. Parameter Tuning
- Use EM algorithm for automatic parameter estimation
- Validate parameters on labeled data when available
- Monitor convergence of EM algorithm

### 4. Threshold Selection
- Use validation data to select optimal threshold
- Consider precision/recall trade-offs
- Apply different thresholds for different use cases

### 5. Performance Optimization
- Profile blocking effectiveness
- Monitor memory usage with large datasets
- Use appropriate batch sizes for your system

## Limitations and Considerations

### Current Limitations
- No support for phonetic similarity (Soundex, Metaphone)
- Limited to string similarity functions
- No machine learning-based similarity
- Basic logging system

### Scalability Considerations
- Memory usage grows with dataset size
- Blocking effectiveness crucial for large datasets
- Consider distributed processing for very large datasets

### Data Quality Impact
- Poor data quality reduces matching accuracy
- Standardization preprocessing recommended
- Handle missing values appropriately


This documentation provides a comprehensive overview of the ReLinker system, its components, and usage patterns. The system provides a solid foundation for record linkage tasks with good performance characteristics and extensibility options.