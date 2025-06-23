using ReLinker.Core;
using System;
using System.Collections.Generic;
using System.Linq;

public static class ReLinkerValidator
{
    public static void ValidateOptions(ReLinkerOptions options)
    {
        if (options == null)
            throw new ArgumentNullException(nameof(options), "ReLinkerOptions cannot be null.");

        if (options.SimilarityFunctions == null || options.SimilarityFunctions.Count == 0)
            throw new ArgumentException("At least one similarity function must be provided.");

        if (options.MProbs == null || options.UProbs == null)
            throw new ArgumentException("MProbs and UProbs must be provided.");

        if (options.MProbs.Length != options.SimilarityFunctions.Count)
            throw new ArgumentException("MProbs count must match the number of similarity functions.");

        if (options.UProbs.Length != options.SimilarityFunctions.Count)
            throw new ArgumentException("UProbs count must match the number of similarity functions.");

        if (options.BatchSize <= 0)
            throw new ArgumentException("BatchSize must be greater than zero.");

        if (options.MatchThreshold < 0 || options.MatchThreshold > 1)
            throw new ArgumentException("MatchThreshold must be between 0 and 1.");
    }
}
