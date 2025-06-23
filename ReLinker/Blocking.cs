using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ReLinker
{
    public class BlockingRule
    {
        public string Name { get; set; }
        public Func<Record, string> RuleFunc { get; set; }

        public BlockingRule(string name, Func<Record, string> ruleFunc)
        {
            Name = name;
            RuleFunc = ruleFunc ?? throw new ArgumentNullException(nameof(ruleFunc));
        }
    }

    public class ScoredPair
    {
        public Record Record1 { get; set; }
        public Record Record2 { get; set; }
        public double Score { get; set; }
    }

    public class SimilarityFunction
    {
        public string FieldName { get; set; }
        public Func<Record, Record, double> Compute { get; set; }
    }

    public class BlockingHelper
    {
        private readonly ILogger<BlockingHelper> _logger;

        public BlockingHelper(ILogger<BlockingHelper> logger)
        {
            _logger = logger;
        }

        public List<BlockingRule> LoadBlockingRulesFromConfig(List<string> fields)
        {
            var rules = fields.Select(field => new BlockingRule(field, r => r.Fields.GetValueOrDefault(field, ""))).ToList();
            _logger.LogInformation("Loaded {Count} blocking rules from config.", rules.Count);
            return rules;
        }

        public IEnumerable<(Record, Record)> GenerateCandidatePairsInBatches(
            IEnumerable<Record> records,
            List<BlockingRule> rules,
            int batchSize)
        {
            var recordList = records.ToList();
            for (int i = 0; i < recordList.Count; i += batchSize)
            {
                var batch = recordList.Skip(i).Take(batchSize).ToList();
                var pairs = new List<(Record, Record)>();

                Parallel.ForEach(batch, record1 =>
                {
                    foreach (var record2 in recordList)
                    {
                        if (string.Compare(record1.Id, record2.Id) >= 0) continue;

                        if (rules.Any(rule => rule.RuleFunc(record1) == rule.RuleFunc(record2)))
                        {
                            lock (pairs)
                            {
                                pairs.Add((record1, record2));
                            }
                        }
                    }
                });

                _logger.LogInformation("Generated {Count} candidate pairs in batch starting at index {StartIndex}.", pairs.Count, i);

                foreach (var pair in pairs)
                    yield return pair;
            }
        }
    }
}
