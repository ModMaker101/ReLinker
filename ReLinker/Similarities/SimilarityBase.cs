using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReLinker.Similarities
{
    public abstract class SimilarityBase : ISimilarity
    {
        protected readonly Dictionary<string, double> _idf;
        
        public SimilarityBase(Dictionary<string, double> idf)
        {
            _idf = idf;
        }

        internal double GetIdf(string token) 
        {
            return _idf.TryGetValue(token, out var value) ? value : 1.0;
        }

        public abstract double Compute(string s1, string s2);
    }
}