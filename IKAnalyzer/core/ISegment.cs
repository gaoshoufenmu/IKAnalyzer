using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKAnalyzer.core
{
    public interface ISegment
    {
        void Analyze(AnalyzeContext context);
        void Reset();
    }
}
