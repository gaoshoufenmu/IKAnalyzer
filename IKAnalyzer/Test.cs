using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace IKAnalyzer
{
    public class Test
    {
        public static void Segment(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;


            var ms = new MemoryStream(Encoding.UTF8.GetBytes(input));
            var ik_segmenter = new core.IKSegmenter(new StreamReader(ms), true);

            var lex = ik_segmenter.Next();
            while(lex != null)
            {
                Console.WriteLine(lex);
                lex = ik_segmenter.Next();
            }
        }
    }
}
