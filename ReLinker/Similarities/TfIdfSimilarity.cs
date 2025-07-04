using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReLinker.Similarities
{
    public class TfIdfSimilarity : SimilarityBase
    {
        public TfIdfSimilarity(Dictionary<string, double> idf) : base(idf) { }
        public override double Compute(string s1, string s2)
            => ComputeTfIdfSimilarity(s1, s2);
        private double ComputeTfIdfSimilarity(string s1, string s2)
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
                    double idfVal = _idf.ContainsKey(token) ? _idf[token] : 0;
                    double v1 = tf1.ContainsKey(token) ? tf1[token] * idfVal : 0;
                    double v2 = tf2.ContainsKey(token) ? tf2[token] * idfVal : 0;
                    dot += v1 * v2;
                    norm1 += v1 * v1;
                    norm2 += v2 * v2;
                }

                double result = (norm1 == 0 || norm2 == 0) ? 0 : dot / (Math.Sqrt(norm1) * Math.Sqrt(norm2));
                return result;
            }
            catch (Exception ex)
            {
                return 0.0;
            }
        }
    }
}
