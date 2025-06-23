using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReLinker.Core
{
    public interface IReLinker
    {
        Task<Dictionary<string, List<string>>> LinkRecordsAsync(ReLinkerOptions options);
        Dictionary<string, List<string>> LinkRecords(ReLinkerOptions options);

        Task<IEnumerable<(Record, Record)>> GenerateCandidatePairsAsync(ReLinkerOptions options);
        Task<List<ScoredPair>> ScoreCandidatePairsAsync(ReLinkerOptions options);

        Task<(double[] mProbs, double[] uProbs)> EstimateParametersAsync(ReLinkerOptions options);

        void ValidateOptions(ReLinkerOptions options);

        Task<List<List<Record>>> LinkRecordsWithDetailsAsync(ReLinkerOptions options);
    }
}
