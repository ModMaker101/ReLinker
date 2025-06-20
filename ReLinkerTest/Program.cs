using System;
using System.Collections.Generic;
using System.Linq;
using RecordLink;

namespace RecordLinkTestApp
{
    class Program
    {
        static void Main(string[] args)
        {
            // Sample records
            var records = new List<Record>
            {
                new Record("1", new Dictionary<string, string> { { "name", "Alice Smith" }, { "city", "Toronto" } }),
                new Record("2", new Dictionary<string, string> { { "name", "Alicia Smith" }, { "city", "Toronto" } }),
                new Record("3", new Dictionary<string, string> { { "name", "Bob Jones" }, { "city", "Vancouver" } }),
                new Record("4", new Dictionary<string, string> { { "name", "Robert Jones" }, { "city", "Vancouver" } })
            };

            // Define blocking rules
            var blockingRules = new List<BlockingRule>
            {
                new BlockingRule("City", r => r.Fields["city"])
            };

            // Define similarity functions
            var similarityFunctions = new List<SimilarityFunction>
            {
                new SimilarityFunction
                {
                    FieldName = "name",
                    Compute = (r1, r2) =>
                    {
                        string name1 = r1.Fields["name"];
                        string name2 = r2.Fields["name"];
                        return JaroWinkler(name1, name2);
                    }
                }
            };

            // Generate candidate pairs
            var candidatePairs = BlockingHelper.GenerateCandidatePairsInBatches(records, blockingRules, batchSize: 2).ToList();

            // Initial probabilities
            double[] mProbs = { 0.9 };
            double[] uProbs = { 0.1 };

            // Score pairs
            var scoredPairs = MatchScorer.Score(candidatePairs, similarityFunctions, mProbs, uProbs);

            // Print results
            Console.WriteLine("Matched Pairs and Scores:");
            foreach (var pair in scoredPairs)
            {
                Console.WriteLine($"{pair.Record1.Id} - {pair.Record2.Id}: Score = {pair.Score:F4}");
            }
        }

        // Simple Jaro-Winkler similarity implementation
        static double JaroWinkler(string s1, string s2)
        {
            if (s1 == s2) return 1.0;
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

            int matchDistance = Math.Max(s1.Length, s2.Length) / 2 - 1;
            bool[] s1Matches = new bool[s1.Length];
            bool[] s2Matches = new bool[s2.Length];

            int matches = 0;
            for (int i = 0; i < s1.Length; i++)
            {
                int start = Math.Max(0, i - matchDistance);
                int end = Math.Min(i + matchDistance + 1, s2.Length);
                for (int j = start; j < end; j++)
                {
                    if (s2Matches[j]) continue;
                    if (s1[i] != s2[j]) continue;
                    s1Matches[i] = true;
                    s2Matches[j] = true;
                    matches++;
                    break;
                }
            }

            if (matches == 0) return 0.0;

            double t = 0;
            int k = 0;
            for (int i = 0; i < s1.Length; i++)
            {
                if (!s1Matches[i]) continue;
                while (!s2Matches[k]) k++;
                if (s1[i] != s2[k]) t++;
                k++;
            }

            t /= 2.0;
            double jaro = ((matches / (double)s1.Length) + (matches / (double)s2.Length) + ((matches - t) / matches)) / 3.0;

            // Jaro-Winkler adjustment
            int prefix = 0;
            for (int i = 0; i < Math.Min(4, Math.Min(s1.Length, s2.Length)); i++)
            {
                if (s1[i] == s2[i]) prefix++;
                else break;
            }

            return jaro + 0.1 * prefix * (1 - jaro);
        }
    }
}
