using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReLinker.Core
{
    public interface IReLinker
    {
        Task<Dictionary<string, List<string>>> LinkRecordsAsync(ReLinkerOptions options);
    }
}