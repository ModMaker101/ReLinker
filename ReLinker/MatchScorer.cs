using ReLinker;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;

public class MatchScorer
{
    private readonly ILogger<MatchScorer> _logger;

    public MatchScorer(ILogger<MatchScorer> logger)
    {
        _logger = logger;
    }

    public List<ScoredPair> Score(
        IEnumerable<(Record, Record)> pairs,
        List<SimilarityFunction> functions,
        double[] mProbs,
        double[] uProbs)
    {
        var scoredPairs = new List<ScoredPair>();

        Parallel.ForEach(pairs, pair =>
        {
            var scores = new double[functions.Count];
            for (int i = 0; i < functions.Count; i++)
            {
                scores[i] = functions[i].Compute(pair.Item1, pair.Item2);
            }

            double logLikelihoodRatio = 0;
            for (int i = 0; i < scores.Length; i++)
            {
                double m = mProbs[i];
                double u = uProbs[i];
                double s = scores[i];

                double numerator = m * s + (1 - m) * (1 - s);
                double denominator = u * s + (1 - u) * (1 - s);

                if (numerator > 0 && denominator > 0)
                {
                    logLikelihoodRatio += Math.Log(numerator / denominator);
                }
                else
                {
                    _logger.LogWarning("Skipped log-likelihood contribution due to zero denominator or numerator for field index {Index}", i);
                }
            }

            lock (scoredPairs)
            {
                scoredPairs.Add(new ScoredPair
                {
                    Record1 = pair.Item1,
                    Record2 = pair.Item2,
                    Score = logLikelihoodRatio
                });
            }
        });

        return scoredPairs;
    }

    public (double[], double[]) EstimateParametersWithEM(
        List<ScoredPair> scoredPairs,
        List<SimilarityFunction> functions,
        int maxIterations = 20,
        double convergenceThreshold = 1e-4,
        double[] fieldWeights = null)
    {
        int n = functions.Count;
        double[] mProbs = Enumerable.Repeat(0.9, n).ToArray();
        double[] uProbs = Enumerable.Repeat(0.1, n).ToArray();
        fieldWeights ??= Enumerable.Repeat(1.0, n).ToArray();

        for (int iter = 0; iter < maxIterations; iter++)
        {
            double[] mNumerator = new double[n];
            double[] uNumerator = new double[n];
            double mDenominator = 0;
            double uDenominator = 0;

            Parallel.ForEach(scoredPairs, pair =>
            {
                double[] scores = new double[n];
                for (int i = 0; i < n; i++)
                    scores[i] = functions[i].Compute(pair.Record1, pair.Record2);

                double mProb = 1.0, uProb = 1.0;
                for (int i = 0; i < n; i++)
                {
                    double s = scores[i];
                    mProb *= mProbs[i] * s + (1 - mProbs[i]) * (1 - s);
                    uProb *= uProbs[i] * s + (1 - uProbs[i]) * (1 - s);
                }

                double weight = mProb / (mProb + uProb);

                lock (mNumerator)
                {
                    for (int i = 0; i < n; i++)
                    {
                        mNumerator[i] += weight * scores[i] * fieldWeights[i];
                        uNumerator[i] += (1 - weight) * scores[i] * fieldWeights[i];
                    }
                    mDenominator += weight;
                    uDenominator += (1 - weight);
                }
            });

            bool converged = true;
            for (int i = 0; i < n; i++)
            {
                double newM = mNumerator[i] / (mDenominator + 1e-10);
                double newU = uNumerator[i] / (uDenominator + 1e-10);
                if (Math.Abs(newM - mProbs[i]) > convergenceThreshold ||
                    Math.Abs(newU - uProbs[i]) > convergenceThreshold)
                    converged = false;

                mProbs[i] = newM;
                uProbs[i] = newU;
            }

            if (converged)
            {
                _logger.LogInformation("[EM] Converged at iteration {Iteration}", iter + 1);
                break;
            }
        }

        return (mProbs, uProbs);
    }
}
