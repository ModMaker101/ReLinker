using Microsoft.Extensions.Logging;
using System;
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
        private Dictionary<string, double> _idfData;

        public ReLinkerEngine(
             IDatabaseLoader loader,
             BlockingHelper blockingHelper,
             DisjointSetForest clusterer,
             ILogger<ReLinkerEngine> logger,
             Dictionary<string, double> idfDictionary = null)
        {
            _loader = loader;
            _blockingHelper = blockingHelper;
            _clusterer = clusterer;
            _logger = logger;
            _idfData = idfDictionary; 
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
        private Dictionary<string, double> CalculateIdf(IEnumerable<Record> records)
        {
            var documentFrequency = new Dictionary<string, int>();
            int totalDocuments = 0;

            foreach (var record in records)
            {
                totalDocuments++;

                var uniqueTokensInRecord = new HashSet<string>();

                foreach (var field in record.Fields.Values)
                {
                    foreach (var token in Tokenize(field))
                    {
                        uniqueTokensInRecord.Add(token);
                    }
                }

                foreach (var token in uniqueTokensInRecord)
                {
                    documentFrequency[token] = documentFrequency.GetValueOrDefault(token, 0) + 1;
                }
            }
        }
        /// <summary>
        /// Helper method to tokenize a string into words
        /// </summary>
        /// <param name="text">The text to tokenize</param>
        /// <returns>an enum of normalized tokns</returns>
        private IEnumerable<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Enumerable.Empty<string>();
            }

            return text.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
