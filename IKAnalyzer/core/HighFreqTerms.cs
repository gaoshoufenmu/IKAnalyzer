using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using IKAnalyzer.cfg;

namespace IKAnalyzer.core
{
    /// <summary>
    /// 高频词条识别
    /// </summary>
    public class HighFreqTerms
    {
        public static DAT<int> _trie = new DAT<int>();

        static HighFreqTerms()
        {
            Load(DefaultConfig.Instance.HighFreqPath);
        }

        private static void Load(string path)
        {
            if (LoadDat(path)) return;

            var dict = new SortedDictionary<string, int>(StrComparer.Default);

            try
            {
                foreach(var line in File.ReadLines(path))
                {
                    var segs = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (segs.Length != 2) continue;

                    dict[segs[0]] = int.Parse(segs[1]);
                }

                _trie.Build(dict);

            }
            catch(Exception e)
            {

            }
        }

        private static bool LoadDat(string path)
        {
            try
            {
                var ba = ByteArray.Create(path + Predefine.BIN_EXT);
                if (ba == null) return false;

                var size = ba.NextInt();
                var freqs = new int[size];

                for(int i = 0; i < size; i++)
                {
                    freqs[i] = ba.NextInt();
                }

                return _trie.Load(ba, freqs) && !ba.HasMore();
            }
            catch(Exception e)
            {
                return false;
            }
        }
        private static void SaveDat(string path, SortedDictionary<string, int> dict)
        {
            var fs = new FileStream(path + Predefine.BIN_EXT, FileMode.Create, FileAccess.Write);

            try
            {
                var bytes = BitConverter.GetBytes(dict.Count);
                fs.Write(bytes, 0, 4);

                foreach(var p in dict)
                {
                    bytes = BitConverter.GetBytes(p.Value);
                    fs.Write(bytes, 0, 4);
                }

                _trie.Save(fs);
            }
            catch(Exception e)
            { }
            finally
            {
                fs.Close();
            }
        }

        public static int GetFreq(string key) => _trie.GetOrDefault(key);
    }
}
