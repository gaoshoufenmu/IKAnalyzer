using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKAnalyzer.core
{
    public class IntReverseComparer : Comparer<int>
    {
        public override int Compare(int x, int y)
        {
            if (x < y) return 1;
            if (x > y) return -1;
            return 0;
        }
    }

    public class StrComparer : Comparer<string>
    {
        public override int Compare(string x, string y)
        {
            var lt = x.Length > y.Length;
            int min_len = lt ? y.Length : x.Length;

            for (int i = 0; i < min_len; i++)
            {
                if (x[i] < y[i])
                    return -1;
                else if (x[i] > y[i])
                    return 1;
            }
            if (x.Length == y.Length)
                return 0;
            return lt ? 1 : -1;
        }

        public static readonly StrComparer Default = new StrComparer();
    }
}
