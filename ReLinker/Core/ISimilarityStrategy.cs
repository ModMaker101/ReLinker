using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReLinker.Core
{
    public interface ISimilaritySimularity
    {
        double Compute(string s1, string s2, Dictionary<string, double> idf);
    }
    public class LevenshteinSimularity : ISimilaritySimularity
    {
        private readonly Similarity _similarity;
        public LevenshteinSimularity(Similarity similarity) => _similarity = similarity;

        public double Compute(string s1, string s2, Dictionary<string, double> idf)
            => _similarity.LevenshteinSimilarity(s1, s2, idf);
    }

    public class JaroSimularity : ISimilaritySimularity
    {
        private readonly Similarity _similarity;
        public JaroSimularity(Similarity similarity) => _similarity = similarity;

        public double Compute(string s1, string s2, Dictionary<string, double> idf)
            => _similarity.JaroSimilarity(s1, s2, idf);
    }

    public class TfIdfSimularity : ISimilaritySimularity
    {
        private readonly Similarity _similarity;
        public TfIdfSimularity(Similarity similarity) => _similarity = similarity;

        public double Compute(string s1, string s2, Dictionary<string, double> idf)
            => _similarity.TfIdfSimilarity(s1, s2, idf);
    }

}
