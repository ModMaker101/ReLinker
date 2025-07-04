using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReLinker.Similarities
{
    public class LevenshteinSimilarity : SimilarityBase
    {
        public LevenshteinSimilarity(Dictionary<string, double> idf) : base(idf) { }
        
        public override double Compute(string s1, string s2)
            => ComputeLevenshteinSimilarity(s1, s2);

        private double ComputeLevenshteinSimilarity(string s1, string s2)
        {
            try
            {
                var tokens1 = s1.ToLower().Split(' ');
                var tokens2 = s2.ToLower().Split(' ');
                int len1 = tokens1.Length;
                int len2 = tokens2.Length;

                double[,] dp = new double[len1 + 1, len2 + 1];

                for (int i = 0; i <= len1; i++)
                    dp[i, 0] = i == 0 ? 0 : dp[i - 1, 0] + GetIdf(tokens1[i - 1]);
                for (int j = 0; j <= len2; j++)
                    dp[0, j] = j == 0 ? 0 : dp[0, j - 1] + GetIdf(tokens2[j - 1]);

                for (int i = 1; i <= len1; i++)
                {
                    for (int j = 1; j <= len2; j++)
                    {
                        double cost = tokens1[i - 1] == tokens2[j - 1] ? 0 :
                            Math.Max(GetIdf(tokens1[i - 1]), GetIdf(tokens2[j - 1]));
                        dp[i, j] = Math.Min(
                            dp[i - 1, j] + GetIdf(tokens1[i - 1]),
                            Math.Min(
                                dp[i, j - 1] + GetIdf(tokens2[j - 1]),
                                dp[i - 1, j - 1] + cost
                            )
                        );
                    }
                }

                double maxCost = tokens1.Sum(t => GetIdf(t)) + tokens2.Sum(t => GetIdf(t));
                double result = 1.0 - (dp[len1, len2] / maxCost);

                return result;
            }
            catch (Exception ex)
            {
                return 0.0;
            }
        }
    }
}
