using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKAnalyzer.core
{
    public class CJKSegmenter : ISegment
    {
        public const string SEGMENTER_NAME = "CJK_SEGMENTER";

        private List<Hit> _hits;

        public CJKSegmenter() => _hits = new List<Hit>();

        public void Analyze(AnalyzeContext context)
        {
            if(context.CurrentCharType != CharUtil.CHAR_USELESS)
            {
                if(_hits.Count > 0)
                {
                    var hits = _hits.ToArray();
                    _hits.Clear();                  // 清空列表，用于保存新一轮的hit对象
                    for(int i = 0; i < hits.Length; i++)
                    {
                        var hit = hits[i];
                        hit = DictTrie.Instance.MatchWithHit(context.CharBuff, context.Cursor, hit);
                        if(hit.IsMatch)
                        {
                            var lex = new Lexeme(context.BuffOffset, hit.Begin, context.Cursor - hit.Begin + 1, Lexeme.TYPE_CN_WORD);
                            context.AddLexeme(lex);

                            if (hit.IsPrefix)   // 是词前缀，hit需要继续匹配，保留
                            {
                                _hits.Add(hit);
                            }                                
                        }
                        else if(hit.IsPrefix)   // !hit.IsUnmatch
                        {
                            // hit 前缀匹配，需要继续匹配，保留
                            _hits.Add(hit);
                        }
                    }
                }

                // 再对当前指针位置的字符进行单字匹配
                var singleCharHit = DictTrie.Instance.MatchWithMainDict(context.CharBuff, context.Cursor, 1);
                if (singleCharHit.IsMatch)
                {
                    var lex = new Lexeme(context.BuffOffset, context.Cursor, 1, Lexeme.TYPE_CN_WORD);
                    context.AddLexeme(lex);

                    if (singleCharHit.IsPrefix)
                        _hits.Add(singleCharHit);
                }
                else if (singleCharHit.IsPrefix)
                    _hits.Add(singleCharHit);
            }
            else
            {
                // 遇到CHAR_USELESS字符，清空队列
                _hits.Clear();
            }

            if (context.IsBuffConsumed())   // 缓冲区已经读完
                _hits.Clear();
            else
                context.LockBuffer(SEGMENTER_NAME);
        }

        public void Reset() => _hits.Clear();
    }
}
