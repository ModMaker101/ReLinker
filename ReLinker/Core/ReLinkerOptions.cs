using System.Collections.Generic;

namespace ReLinker.Core
{
    public class ReLinkerOptions
    {
        public List<string> BlockingFields { get; set; }
        public List<SimilarityFunction> SimilarityFunctions { get; set; }
        public double[] MProbs { get; set; }
        public double[] UProbs { get; set; }
        public int BatchSize { get; set; } = 100;
        public double MatchThreshold { get; set; } = 0.0;
    }
}