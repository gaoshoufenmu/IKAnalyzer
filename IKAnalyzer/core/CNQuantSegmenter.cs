using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKAnalyzer.core
{
    public class CNQuantSegmenter : ISegment
    {
        private const string SEGMENTER_NAME = "CN_QUANT_SEGMENTER";
        /// <summary>
        /// 中文数词
        /// </summary>
        private const string CN_NUM = "○〇一二三四五六七八九十百千万亿零壹贰叁肆伍陆柒捌玖拾佰仟萬億兆卅廿";
        private static HashSet<char> _cnNums = new HashSet<char>(CN_NUM);

        private int _start;
        private int _end;

        /// <summary>
        /// 待成量词 hit列表
        /// 存储了一些未成形量词的 hit 对象
        /// </summary>
        private List<Hit> _hits;

        public CNQuantSegmenter()
        {
            _end = _start = -1;
            _hits = new List<Hit>();
        }

        public void Analyze(AnalyzeContext context)
        {
            ProcessNum(context);
            ProcessQuant(context);
            if (_start == -1 && _end == -1 && _hits.Count == 0)     // 如果当前数量词处理完毕，且没有待成量词的hit，则释放占用的缓冲区
                context.UnlockBuffer(SEGMENTER_NAME);
            else
                context.LockBuffer(SEGMENTER_NAME); 
        }

        /// <summary>
        /// 处理中文数词
        /// </summary>
        /// <param name="context"></param>
        private void ProcessNum(AnalyzeContext context)
        {
            if(_start == -1 && _end == -1)      // 当前尚未开始处理中文数词
            {
                if (context.CurrentCharType == CharUtil.CHAR_CHINESE && _cnNums.Contains(context.CurrentChar))
                    _end = _start = context.Cursor;     // 如果遇到中文数词，记录起始位置
            }
            else
            {
                if (context.CurrentCharType == CharUtil.CHAR_CHINESE && _cnNums.Contains(context.CurrentChar))
                    _end = context.Cursor;          // 继续匹配到中文数词，更新结束位置
                else
                {
                    // 中文数词结束，输出词元
                    var lex = new Lexeme(context.BuffOffset, _start, _end - _start + 1, Lexeme.TYPE_CN_NUM);
                    context.AddLexeme(lex);
                    _end = _start = -1;
                }
            }

            if(context.IsBuffConsumed())    // 缓冲区如果读取完毕
            {
                if(_start != -1 && _end != -1)
                {
                    // 当前仍在处理中文数词，那么输出词元
                    var lex = new Lexeme(context.BuffOffset, _start, _end - _start + 1, Lexeme.TYPE_CN_NUM);
                    context.AddLexeme(lex);
                    _end = _start = -1;
                }
            }
        }

        /// <summary>
        /// 处理中文量词
        /// </summary>
        /// <param name="context"></param>
        private void ProcessQuant(AnalyzeContext context)
        {
            if (!NeedQuantScan(context))    // 如果不需要量词扫描，则直接返回
                return;

            if(context.CurrentCharType == CharUtil.CHAR_CHINESE)
            {
                if(_hits.Count > 0)     // 如果前面积累了一些尚未成形的量词，则优先处理这些
                {
                    var hits = _hits.ToArray();
                    _hits.Clear();                  // 先清空，然后再保存那些本轮尚未处理掉的未成形量词（比如新的尚未成形的量词）
                    for(int i = 0; i < hits.Length; i++)
                    {
                        var hit = hits[i];
                        hit = DictTrie.Instance.MatchWithHit(context.CharBuff, context.Cursor, hit);
                        if (hit.IsMatch)     // 当前尚未成形的量词 hit 加上 context.Cursor 开始的若干字符形成一个成形的量词
                        {
                            var lex = new Lexeme(context.BuffOffset, hit.Begin, context.Cursor - hit.Begin + 1, Lexeme.TYPE_CN_QUANT);
                            context.AddLexeme(lex);
                            if (hit.IsPrefix)   // 这个新成形的量词如果同时还是某个量词的前缀，则添加到 hit 列表（最大化匹配）
                                _hits.Add(hit);
                        }
                        else if (hit.IsPrefix)  // 当前尚未成形的量词 hit 加上 context.Cursor 开始的若干字符形成某个量词的前缀，则添加到 hit 列表
                            _hits.Add(hit);
                    }
                }
                // 除了与前面的尚未成形的量词进行联合，context.Cursor开始的字符另外还需要单字匹配，以发现更多可能的量词
                var singleHit = DictTrie.Instance.MatchWithQuantDict(context.CharBuff, context.Cursor, 1);
                if (singleHit.IsMatch)   // 首字成量词
                {
                    // 输出此单字量词
                    var lex = new Lexeme(context.BuffOffset, context.Cursor, 1, Lexeme.TYPE_CN_QUANT);
                    context.AddLexeme(lex);

                    if (singleHit.IsPrefix)     // 如果当前单字同时还是另一个量词的前缀
                        _hits.Add(singleHit);
                }
                else if (singleHit.IsPrefix)     // 当前单字虽未独立成量词，但是是另一个量词的前缀
                    _hits.Add(singleHit);
            }
            else    // 当前字符不是中文字符，则清空未成形的量词，因为已经不可能成量词了
            {
                _hits.Clear();
            }

            if (context.IsBuffConsumed())   // 如果缓冲区已经读取完毕
                _hits.Clear();              // 清空尚未成形的量词，因为已经没机会成量词了
        }

        /// <summary>
        /// 判断是否需要扫描量词
        /// </summary>
        /// <param name="context"></param>
        /// <returns>是否需要扫描量词</returns>
        private bool NeedQuantScan(AnalyzeContext context)
        {
            // 如果当前正在处理中文数词，或者仍有带处理量词 hit
            if ((_start != -1 && _end != -1) || _hits.Count > 0)
                return true;
            else
            {
                // 紧邻当前的上一个词元
                if(context.RawLexemes.Size > 0)
                {
                    var lex = context.RawLexemes.PeekLast();
                    // 如果是中文数词，或者是阿拉伯数词
                    if(Lexeme.TYPE_CN_NUM == lex.Type || Lexeme.TYPE_ARABIC == lex.Type)
                    {
                        // 如果上一个词元与当前正要处理的字符无缝连接，且没有重叠
                        if (lex.Begin + lex.Length == context.Cursor)
                            return true;
                    }
                }
            }
            return false;
        }

        public void Reset()
        {
            _end = _start = -1;
            _hits.Clear();
        }
    }
}
