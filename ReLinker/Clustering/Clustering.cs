using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ReLinker
{
    public class DisjointSetForest
    {
        private readonly Dictionary<string, string> parent = new();
        private readonly ILogger<DisjointSetForest> _logger;

        public DisjointSetForest(ILogger<DisjointSetForest> logger)
        {
            _logger = logger;
        }

        public string Find(string x)
        {
            if (!parent.ContainsKey(x))
            {
                parent[x] = x;
                _logger.LogDebug("Added new element '{Element}' as its own parent.", x);
            }

            if (parent[x] != x)
            {
                parent[x] = Find(parent[x]);
                _logger.LogTrace("Path compression for '{Element}', new parent: '{Parent}'.", x, parent[x]);
            }

            return parent[x];
        }

        public void MergeEntities(string x, string y)
        {
            var px = Find(x);
            var py = Find(y);

            if (px != py)
            {
                parent[px] = py;
                _logger.LogInformation("Merged sets: '{Set1}' -> '{Set2}'", px, py);
            }
            else
            {
                _logger.LogDebug("No merge needed: '{Set1}' and '{Set2}' are already in the same set.", px, py);
            }
        }

        public Dictionary<string, List<string>> GetClusters()
        {
            var clusters = new Dictionary<string, List<string>>();
            foreach (var key in parent.Keys)
            {
                var root = Find(key);
                if (!clusters.ContainsKey(root))
                    clusters[root] = new List<string>();
                clusters[root].Add(key);
            }

            _logger.LogInformation("Generated {ClusterCount} clusters.", clusters.Count);
            return clusters;
        }
    }
}
