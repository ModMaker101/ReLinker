using System;
using System.Collections.Generic;

namespace ReLinker.Similarities
{
    public interface ISimilarity
    {
        double Compute(string s1, string s1); // idf parm removed
    }
}
