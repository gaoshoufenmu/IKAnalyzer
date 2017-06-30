using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using IKAnalyzer.cfg;

namespace IKAnalyzer.core
{
    public class IKSegmenter
    {
        private IConfig _cfg;
        private StreamReader _reader;
        private AnalyzeContext _context;
        private IKArbitrator _arbitrator;
        private List<ISegment> _segmenters;

        public IKSegmenter(StreamReader reader, bool useSmart)
        {
            _reader = reader;
            _cfg = DefaultConfig.Instance;
            _cfg.UseSmart = useSmart;
            Init();
        }

        private void Init()
        {
            DictTrie.Init(_cfg);
            _context = new AnalyzeContext(_cfg);
            _arbitrator = new IKArbitrator();

            _segmenters = new List<ISegment>(3);
            _segmenters.Add(new EnglishSegmenter());
            _segmenters.Add(new CNQuantSegmenter());
            _segmenters.Add(new CJKSegmenter());
        }

        public Lexeme Next()
        {
            var lex = _context.GetNextLexeme();     // 获取下一个词元
            while(lex == null)                      // 如果为null，说明结果集中的分词已经使用完毕，需要继续填充缓冲区了
            {
                int buffSize = _context.FillBuff(_reader);
                if(buffSize <= 0)       // 原始待分词的内容已经读取完毕，本次分词结束
                {
                    _context.Reset();
                    return null;
                }
                else    // 使用新数据填充了缓冲区，则需要进行必要的重置操作
                {
                    _context.InitCursor();  // 重置context 当前处理字符的位置指针
                    // 分析 context 缓冲区中的内容，轮流使用各分词器尝试分词
                    do
                    {
                        foreach (var segmenter in _segmenters)
                            segmenter.Analyze(_context);

                        // 缓冲区快读取完毕时，退出循环，以免出现缓冲区右边界上的不完整分词
                        // 即，缓冲区最后的一些字符留到下轮读取更多的字符后再进行分词
                        if (_context.NeedRefillBuff())  
                            break;
                    } while (_context.MoveCursor());    // 只要当前处理字符的位置仍在缓冲区内，就继续循环体操作

                    foreach (var segmenter in _segmenters)  // 重置分词器，为下轮循环做准备
                        segmenter.Reset();
                }

                // 本轮缓冲区分词结束后进行歧义处理
                _arbitrator.Process(_context, _cfg.UseSmart);
                // 输出结果到结果集 _context._results
                _context.OutputResults();   
                // 更新缓冲区的 offset
                _context.UpdateOffset();

                lex = _context.GetNextLexeme();
            }
            return lex;
        }

        /// <summary>
        /// 使用新的待分词内容来重置分词器
        /// </summary>
        /// <param name="sr"></param>
        public void Reset(StreamReader sr)
        {
            _reader = sr;
            _context.Reset();
            foreach (var segmenter in _segmenters)
                segmenter.Reset();
        }
    }
}
