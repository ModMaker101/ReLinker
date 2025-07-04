using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReLinker.Similarities
{
    public class JaroSimilarity : SimilarityBase
    {
        public JaroSimilarity(Dictionary<string, double> idf) : base(idf) { }
        public override double Compute(string s1, string s2)
            => ComputeJaroSimilarity(s1, s2);

        private double ComputeJaroSimilarity(string s1, string s2)
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
                        matchedWeight += GetIdf(tokens1[i]);
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
                        transpositions += GetIdf(tokens1[i]);
                    k++;
                }

                double totalWeight1 = tokens1.Sum(t => GetIdf(t));
                double totalWeight2 = tokens2.Sum(t => GetIdf(t));
                double result = (matchedWeight / totalWeight1 + matchedWeight / totalWeight2 + (matchedWeight - transpositions / 2.0) / matchedWeight) / 3.0;
                
                return result;
            }
            catch (Exception ex)
            {
                
                return 0.0;
            }
        }
    }
}
