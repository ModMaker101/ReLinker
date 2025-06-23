using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReLinker
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

        public void ValidateOptions(ReLinkerOptions options)
        {
            ReLinkerValidator.ValidateOptions(options);
        }

        public async Task<Dictionary<string, List<string>>> LinkRecordsAsync(ReLinkerOptions options)
        {
            ValidateOptions(options);
            var records = await _loader.LoadRecordsAsync();
            return LinkInternal(records, options);
        }

        public Dictionary<string, List<string>> LinkRecords(ReLinkerOptions options)
        {
            ValidateOptions(options);
            var records = _loader.LoadRecords();
            return LinkInternal(records, options);
        }

        public async Task<IEnumerable<(Record, Record)>> GenerateCandidatePairsAsync(ReLinkerOptions options)
        {
            var records = await _loader.LoadRecordsAsync();
            var blockingRules = _blockingHelper.LoadBlockingRulesFromConfig(options.BlockingFields);
            return _blockingHelper.GenerateCandidatePairsInBatches(records, blockingRules, options.BatchSize);
        }

        public async Task<List<ScoredPair>> ScoreCandidatePairsAsync(ReLinkerOptions options)
        {
            var pairs = await GenerateCandidatePairsAsync(options);
            return _scorer.Score(pairs, options.SimilarityFunctions, options.MProbs, options.UProbs);
        }

        public async Task<(double[] mProbs, double[] uProbs)> EstimateParametersAsync(ReLinkerOptions options)
        {
            var scoredPairs = await ScoreCandidatePairsAsync(options);
            return _scorer.EstimateParametersWithEM(scoredPairs, options.SimilarityFunctions);
        }

        public async Task<List<List<Record>>> LinkRecordsWithDetailsAsync(ReLinkerOptions options)
        {
            var records = await _loader.LoadRecordsAsync();
            var idToRecord = records.ToDictionary(r => r.Id);
            var clusters = LinkInternal(records, options);
            return clusters.Values
                .Select(cluster => cluster.Select(id => idToRecord[id]).ToList())
                .ToList();
        }

        private Dictionary<string, List<string>> LinkInternal(List<Record> records, ReLinkerOptions options)
        {
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
