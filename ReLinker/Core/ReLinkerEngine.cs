using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ReLinker.Core
{
    public class ReLinkerEngine : IReLinker
    {
        private readonly IDatabaseLoader _loader;
        private readonly MatchScorer _scorer;
        private readonly BlockingHelper _blockingHelper;
        private readonly DisjointSetForest _clusterer;
        private readonly ILogger<ReLinkerEngine> _logger;

        public ReLinkerEngine(
            IDatabaseLoader loader,
            MatchScorer scorer,
            BlockingHelper blockingHelper,
            DisjointSetForest clusterer,
            ILogger<ReLinkerEngine> logger)
        {
            _loader = loader;
            _scorer = scorer;
            _blockingHelper = blockingHelper;
            _clusterer = clusterer;
            _logger = logger;
        }

        public async Task<Dictionary<string, List<string>>> LinkRecordsAsync(ReLinkerOptions options)
        {
            var records = await _loader.LoadRecordsAsync();
            var blockingRules = _blockingHelper.LoadBlockingRulesFromConfig(options.BlockingFields);
            var candidatePairs = _blockingHelper.GenerateCandidatePairsInBatches(records, blockingRules, options.BatchSize);
            var scoredPairs = _scorer.Score(candidatePairs, options.SimilarityFunctions, options.MProbs, options.UProbs);

            foreach (var pair in scoredPairs.Where(p => p.Score > options.MatchThreshold))
            {
                _clusterer.MergeEntities(pair.Record1.Id, pair.Record2.Id);
            }

            return _clusterer.GetClusters();
        }
    }
}