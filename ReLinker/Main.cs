using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ReLinker
{
    public class Record
    {
        public string Id { get; set; }
        public Dictionary<string, string> Fields { get; set; }

        public Record(string id, Dictionary<string, string> fields)
        {
            Id = id;
            Fields = fields;
        }
    }

    public class FieldComparison
    {
        public string FieldName { get; set; }
        public Func<string, string, double> SimilarityFunc { get; set; }
        public double MProbability { get; set; }
        public double UProbability { get; set; }
    }
}