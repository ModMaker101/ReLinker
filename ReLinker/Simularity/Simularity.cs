using Microsoft.Extensions.Logging;
using ReLinker;
using ReLinker.Core;
using System;
using System.Collections.Generic;
using System.Linq;

public class Similarity
{
    private readonly ILogger<Similarity> _logger;

    public Similarity(ILogger<Similarity> logger)
    {
        _logger = logger;
    }

    public double LevenshteinSimilarity(string s1, string s2, Dictionary<string, double> idf)
    {
        try
        {
            var tokens1 = s1.ToLower().Split(' ');
            var tokens2 = s2.ToLower().Split(' ');

            int len1 = tokens1.Length, len2 = tokens2.Length;
            double[,] dp = new double[len1 + 1, len2 + 1];

            for (int i = 0; i <= len1; i++)
                dp[i, 0] = i == 0 ? 0 : dp[i - 1, 0] + GetIdf(tokens1[i - 1], idf);

            for (int j = 0; j <= len2; j++)
                dp[0, j] = j == 0 ? 0 : dp[0, j - 1] + GetIdf(tokens2[j - 1], idf);

            for (int i = 1; i <= len1; i++)
            {
                for (int j = 1; j <= len2; j++)
                {
                    double cost = tokens1[i - 1] == tokens2[j - 1] ? 0 :
                        Math.Max(GetIdf(tokens1[i - 1], idf), GetIdf(tokens2[j - 1], idf));

                    dp[i, j] = Math.Min(
                        dp[i - 1, j] + GetIdf(tokens1[i - 1], idf), // delete
                        Math.Min(
                            dp[i, j - 1] + GetIdf(tokens2[j - 1], idf), // insert
                            dp[i - 1, j - 1] + cost // substitute
                        )
                    );
                }
            }

            double maxCost = tokens1.Sum(t => GetIdf(t, idf)) + tokens2.Sum(t => GetIdf(t, idf));
            double result = 1.0 - (dp[len1, len2] / maxCost);
            _logger.LogDebug("Levenshtein similarity between '{S1}' and '{S2}' is {Result}", s1, s2, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing Levenshtein similarity between '{S1}' and '{S2}'", s1, s2);
            return 0.0;
        }
    }

    public double JaroSimilarity(string s1, string s2, Dictionary<string, double> idf)
    {
        try
        {
            var tokens1 = s1.ToLower().Split(' ');
            var tokens2 = s2.ToLower().Split(' ');

            int len1 = tokens1.Length, len2 = tokens2.Length;
            int matchDist = Math.Max(len1, len2) / 2 - 1;

            bool[] t1Matches = new bool[len1];
            bool[] t2Matches = new bool[len2];

            double matchedWeight = 0;
            for (int i = 0; i < len1; i++)
            {
                int start = Math.Max(0, i - matchDist);
                int end = Math.Min(i + matchDist + 1, len2);

                for (int j = start; j < end; j++)
                {
                    if (t2Matches[j] || tokens1[i] != tokens2[j]) continue;
                    t1Matches[i] = t2Matches[j] = true;
                    matchedWeight += GetIdf(tokens1[i], idf);
                    break;
                }
            }

            if (matchedWeight == 0) return 0.0;

            double transpositions = 0;
            int k = 0;
            for (int i = 0; i < len1; i++)
            {
                if (!t1Matches[i]) continue;
                while (!t2Matches[k]) k++;
                if (tokens1[i] != tokens2[k])
                    transpositions += GetIdf(tokens1[i], idf);
                k++;
            }

            double totalWeight1 = tokens1.Sum(t => GetIdf(t, idf));
            double totalWeight2 = tokens2.Sum(t => GetIdf(t, idf));

            double result = (matchedWeight / totalWeight1 + matchedWeight / totalWeight2 + (matchedWeight - transpositions / 2.0) / matchedWeight) / 3.0;
            _logger.LogDebug("Jaro similarity between '{S1}' and '{S2}' is {Result}", s1, s2, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing Jaro similarity between '{S1}' and '{S2}'", s1, s2);
            return 0.0;
        }
    }

    public double TfIdfSimilarity(string s1, string s2, Dictionary<string, double> idf)
    {
        try
        {
            var tokens1 = s1.ToLower().Split(' ');
            var tokens2 = s2.ToLower().Split(' ');

            var tf1 = tokens1.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count() / (double)tokens1.Length);
            var tf2 = tokens2.GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count() / (double)tokens2.Length);

            var allTokens = new HashSet<string>(tf1.Keys);
            allTokens.UnionWith(tf2.Keys);

            double dot = 0, norm1 = 0, norm2 = 0;
            foreach (var token in allTokens)
            {
                double idfVal = idf.ContainsKey(token) ? idf[token] : 0;
                double v1 = tf1.ContainsKey(token) ? tf1[token] * idfVal : 0;
                double v2 = tf2.ContainsKey(token) ? tf2[token] * idfVal : 0;
                dot += v1 * v2;
                norm1 += v1 * v1;
                norm2 += v2 * v2;
            }

            double result = (norm1 == 0 || norm2 == 0) ? 0 : dot / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
            _logger.LogDebug("TF-IDF similarity between '{S1}' and '{S2}' is {Result}", s1, s2, result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing TF-IDF similarity between '{S1}' and '{S2}'", s1, s2);
            return 0.0;
        }
    }

    public class SimilarityFactory
    {
        private readonly Dictionary<Type, ISimilaritySimularity> _strategies = new();

        public void Register<T>(ISimilaritySimularity strategy) where T : ISimilaritySimularity
        {
            _strategies[typeof(T)] = strategy;
        }

        public Func<Record, Record, double> Create<T>(string field, Dictionary<string, double> idf)
            where T : ISimilaritySimularity
        {
            if (!_strategies.TryGetValue(typeof(T), out var strategy))
                throw new InvalidOperationException($"Strategy for {typeof(T).Name} not registered.");

            return (r1, r2) => strategy.Compute(r1.Fields[field], r2.Fields[field], idf);
        }
    }


    private static double GetIdf(string token, Dictionary<string, double> idf)
    {
        return idf.TryGetValue(token, out var value) ? value : 1.0;
    }
}
