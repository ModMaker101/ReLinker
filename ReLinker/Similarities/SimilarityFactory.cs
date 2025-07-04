using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReLinker.Similarities
{
    public class SimilarityFactory
    {
        private readonly Dictionary<Type, ISimilarity> _strategies = new();

        public void Register<T>(ISimilarity strategy) where T : ISimilarity
        {
            _strategies[typeof(T)] = strategy;
        }

        public Func<Record, Record, double> Create<T>(string field)
            where T : ISimilarity
        {
            if (!_strategies.TryGetValue(typeof(T), out var strategy))
                throw new InvalidOperationException($"Strategy for {typeof(T).Name} not registered.");

            return (r1, r2) => strategy.Compute(r1.Fields[field], r2.Fields[field]);
        }
    }
}
