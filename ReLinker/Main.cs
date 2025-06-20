using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static class SimpleLogger
{
    public static void Info(string message) =>
        Console.WriteLine($"[INFO] {DateTime.Now}: {message}");
    public static void Error(string message) =>
        Console.WriteLine($"[ERROR] {DateTime.Now}: {message}");
}

namespace RecordLink
{
    public class Record
    {
        public string Id { get; set; }
        public Dictionary<string, string> Fields { get; set; }

        public Record(string id, Dictionary<string, string> fields)
        {
            Id = id;
            Fields = fields;
        }
    }
    
    public class FieldComparison
    {
        public string FieldName { get; set; }
        public Func<string, string, double> SimilarityFunc { get; set; }
        public double MProbability { get; set; }
        public double UProbability { get; set; }
    }

    public class MatchScorer
    {
        public static List<ScoredPair> Score(
            IEnumerable<(Record, Record)> pairs,
            List<SimilarityFunction> functions,
            double[] mProbs,
            double[] uProbs)
        {
            var scoredPairs = new List<ScoredPair>();

            Parallel.ForEach(pairs, pair =>
            {
                var scores = new double[functions.Count];
                for (int i = 0; i < functions.Count; i++)
                {
                    scores[i] = functions[i].Compute(pair.Item1, pair.Item2);
                }

                double logLikelihoodRatio = 0;
                for (int i = 0; i < scores.Length; i++)
                {
                    double m = mProbs[i];
                    double u = uProbs[i];
                    double s = scores[i];
                    logLikelihoodRatio += Math.Log((m * s + (1 - m) * (1 - s)) / (u * s + (1 - u) * (1 - s)));
                }

                lock (scoredPairs)
                {
                    scoredPairs.Add(new ScoredPair
                    {
                        Record1 = pair.Item1,
                        Record2 = pair.Item2,
                        Score = logLikelihoodRatio
                    });
                }
            });

            return scoredPairs;
        }

        public static (double[], double[]) EstimateParametersWithEM(
            List<ScoredPair> scoredPairs,
            List<SimilarityFunction> functions,
            int maxIterations = 10)
        {
            int n = functions.Count;
            double[] mProbs = Enumerable.Repeat(0.9, n).ToArray();
            double[] uProbs = Enumerable.Repeat(0.1, n).ToArray();

            for (int iter = 0; iter < maxIterations; iter++)
            {
                double[] mNumerator = new double[n];
                double[] uNumerator = new double[n];
                double mDenominator = 0;
                double uDenominator = 0;

                Parallel.ForEach(scoredPairs, pair =>
                {
                    double[] scores = new double[n];
                    for (int i = 0; i < n; i++)
                        scores[i] = functions[i].Compute(pair.Record1, pair.Record2);

                    double mProb = 1.0;
                    double uProb = 1.0;
                    for (int i = 0; i < n; i++)
                    {
                        mProb *= mProbs[i] * scores[i] + (1 - mProbs[i]) * (1 - scores[i]);
                        uProb *= uProbs[i] * scores[i] + (1 - uProbs[i]) * (1 - scores[i]);
                    }

                    double weight = mProb / (mProb + uProb);

                    lock (mNumerator)
                    {
                        for (int i = 0; i < n; i++)
                        {
                            mNumerator[i] += weight * scores[i];
                            uNumerator[i] += (1 - weight) * scores[i];
                        }
                        mDenominator += weight;
                        uDenominator += (1 - weight);
                    }
                });

                for (int i = 0; i < n; i++)
                {
                    mProbs[i] = mNumerator[i] / mDenominator;
                    uProbs[i] = uNumerator[i] / uDenominator;
                }
            }

            return (mProbs, uProbs);
        }
    }
}

public class UnionFind
    {
        private readonly Dictionary<string, string> parent = new();

        public string Find(string x)
        {
            if (!parent.ContainsKey(x)) parent[x] = x;
            if (parent[x] != x) parent[x] = Find(parent[x]);
            return parent[x];
        }

        public void Union(string x, string y)
        {
            var px = Find(x);
            var py = Find(y);
            if (px != py) parent[px] = py;
        }

        public Dictionary<string, List<string>> GetClusters()
        {
            var clusters = new Dictionary<string, List<string>>();
            foreach (var key in parent.Keys)
            {
                var root = Find(key);
                if (!clusters.ContainsKey(root)) clusters[root] = new List<string>();
                clusters[root].Add(key);
            }
            return clusters;
        }
    }

    public static class Similarity
    {
        public static double LevenshteinSimilarity(string s1, string s2, Dictionary<string, double> idf)
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
            return 1.0 - (dp[len1, len2] / maxCost);
        }


        public static double JaroSimilarity(string s1, string s2, Dictionary<string, double> idf)
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

            return (matchedWeight / totalWeight1 + matchedWeight / totalWeight2 + (matchedWeight - transpositions / 2.0) / matchedWeight) / 3.0;
        }

        public static double TfIdfSimilarity(string s1, string s2, Dictionary<string, double> idf)
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

            return (norm1 == 0 || norm2 == 0) ? 0 : dot / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
        }
        private static double GetIdf(string token, Dictionary<string, double> idf)
        {
            return idf.TryGetValue(token, out var value) ? value : 1.0;
        }

    }

