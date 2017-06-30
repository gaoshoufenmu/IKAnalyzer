using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKAnalyzer.core
{
    public class EnglishSegmenter : ISegment
    {
        public const string SEGMENTER_NAME = "english_NUM_SEGMENTER";
        /// <summary>
        /// 连接符号
        /// </summary>
        private static char[] Letter_Connector = new[] { '#', '&', '+', '-', '.', '@', '_' };
        /// <summary>
        /// 数字间符号
        /// </summary>
        private static char[] Num_Connector = new[] { ',', '.' };

        /// <summary>
        /// 英文或者数字组成的混合词元的开始位置，第一个字符的位置（inclusive）
        /// 当值大于0时，表示当前分词器正在处理字符
        /// </summary>
        private int _start;

        /// <summary>
        /// 英文或者数字组成的混合词元的结束位置，最后一个字符的位置（inclusive）
        /// 记录词元中最后一个非连接符的位置
        /// </summary>
        private int _end;

        /// <summary>
        /// 英文词元开始位置，第一个字符的位置（inclusive）
        /// </summary>
        private int _englishStart;
        /// <summary>
        /// 英文词元结束位置，最后一个字符的位置（inclusive）
        /// </summary>
        private int _englishEnd;
        /// <summary>
        /// 数字词元的开始位置，第一个字符位置 （inclusive）
        /// </summary>
        private int _arabicStart;
        /// <summary>
        /// 数字词元的结束位置，最后一个字符位置 （inclusive）
        /// </summary>
        private int _arabicEnd;


        public EnglishSegmenter()
        {
            Reset();
            Array.Sort(Letter_Connector);
            Array.Sort(Num_Connector);
        }

        public void Analyze(AnalyzeContext context)
        {
            // 当前是处理完状态还是正在处理中状态
            // 注意处理的顺序，ProcessMix必须第一处理
            var flag = ProcessMix(context) || ProcessEnglish(context) || ProcessArabic(context);

            if (flag)
                context.LockBuffer(SEGMENTER_NAME);
            else
                context.UnlockBuffer(SEGMENTER_NAME);
        }

        /// <summary>
        /// 处理英文或者数字组成的混合词元
        /// </summary>
        /// <param name="context"></param>
        /// <returns>当前是否是处理完状态还是正在处理中状态</returns>
        private bool ProcessMix(AnalyzeContext context)
        {
            if(_start == -1)    // 当前分词器尚未开始处理混合词元
            {
                if (CharUtil.CHAR_ARABIC == context.CurrentCharType || CharUtil.CHAR_ENGLISH == context.CurrentCharType)
                    _end = _start = context.Cursor;
            }
            else
            {
                // 如果是英文字符或者是数字字符
                if (CharUtil.CHAR_ARABIC == context.CurrentCharType || CharUtil.CHAR_ENGLISH == context.CurrentCharType)
                    _end = context.Cursor;          // 更新混合词元的结束位置
                // 如果是英文连接符
                else if (CharUtil.CHAR_USELESS == context.CurrentCharType && IsLetterConnector(context.CurrentChar))
                    _end = context.Cursor;
                else
                {
                    // 非有效英文或数字字符
                    var lex = new Lexeme(context.BuffOffset, _start, _end - _start + 1, Lexeme.TYPE_ALPHANUM);
                    context.AddLexeme(lex);
                    _end = _start = -1;
                }
            }
            if(context.IsBuffConsumed())
            {
                if(_start != -1 && _end != -1)
                {
                    var lex = new Lexeme(context.BuffOffset, _start, _end - _start + 1, Lexeme.TYPE_ALPHANUM);
                    context.AddLexeme(lex);
                    _end = _start = -1;
                }
            }
            return _start != -1 || _end != -1;
        }

        /// <summary>
        /// 处理英文词元
        /// </summary>
        /// <param name="context"></param>
        /// <returns>当前是否是处理完状态还是正在处理中状态</returns>
        private bool ProcessEnglish(AnalyzeContext context)
        {
            if(_englishStart == -1)       // 当前分词器尚未开始处理英文字符
            {
                if(context.CurrentCharType == CharUtil.CHAR_ENGLISH)
                {
                    _englishStart = context.Cursor;
                    _englishEnd = _englishStart;
                }
            }
            else
            {
                if(context.CurrentCharType == CharUtil.CHAR_ENGLISH)
                {
                    _englishEnd = context.Cursor;
                }
                else            // 遇到非英文字符，则输出英文词元
                {
                    var lex = new Lexeme(context.BuffOffset, _englishStart, _englishEnd - _englishStart + 1, Lexeme.TYPE_ENGLISH);
                    context.AddLexeme(lex);
                    _englishEnd = _englishStart = -1;
                }
            }

            if(context.IsBuffConsumed())    // 如果缓冲区内容读取完毕
            {
                if(_englishStart != -1 && _englishEnd != -1)    // 如果当前正在处理英文词元
                {
                    var lex = new Lexeme(context.BuffOffset, _englishStart, _englishEnd - _englishStart + 1, Lexeme.TYPE_ENGLISH);
                    context.AddLexeme(lex);
                    _englishEnd = _englishStart = -1;
                }
            }

            return _englishStart != -1 || _englishEnd != -1;    // 返回是否需要锁定缓冲区
        }
        /// <summary>
        /// 处理数字词元
        /// </summary>
        /// <param name="context"></param>
        /// <returns>当前是否是处理完状态还是正在处理中状态</returns>
        private bool ProcessArabic(AnalyzeContext context)
        {
            if(_arabicStart == -1)      // 当前分词器尚未开始处理数字字符
            {
                if (context.CurrentCharType == CharUtil.CHAR_ARABIC)
                    _arabicEnd = _arabicStart = context.Cursor;
            }
            else                        // 当前分词器正在处理数字字符
            {
                if (context.CurrentCharType == CharUtil.CHAR_ARABIC)
                    _arabicEnd = context.Cursor;
                else if(context.CurrentCharType == CharUtil.CHAR_USELESS && IsNumConnector(context.CurrentChar))
                {
                    // 遇到数字之间的有效分隔符
                }
                else
                {   // 遇到非数字字符，输出数字词元
                    var lex = new Lexeme(context.BuffOffset, _arabicStart, _arabicEnd - _arabicStart + 1, Lexeme.TYPE_ARABIC);
                    context.AddLexeme(lex);
                    _arabicEnd = _arabicStart = -1;
                }
            }

            if(context.IsBuffConsumed())
            {
                if(_arabicStart != -1 && _arabicEnd != -1)
                {
                    var lex = new Lexeme(context.BuffOffset, _arabicStart, _arabicEnd - _arabicStart + 1, Lexeme.TYPE_ARABIC);
                    context.AddLexeme(lex);
                    _arabicEnd = _arabicStart = -1;
                }
            }

            return _arabicStart != -1 || _arabicEnd != -1;
        }

        public void Reset()
        {
            _start = -1;
            _end = -1;
            _englishEnd = -1;
            _englishStart = -1;
            _arabicEnd = -1;
            _arabicStart = -1;
        }

        private bool BinarySearch(char c, char[] array, int fromInclusive, int toExclusive)
        {
            if (fromInclusive >= toExclusive) return false;

            var m = (fromInclusive + toExclusive) / 2;
            var cm = array[m];

            if (cm == c) return true;

            if (cm < c) return BinarySearch(c, array, m + 1, toExclusive);

            return BinarySearch(c, array, fromInclusive, m);
        }

        private bool IsLetterConnector(char c) => BinarySearch(c, Letter_Connector, 0, Letter_Connector.Length);
        private bool IsNumConnector(char c) => BinarySearch(c, Num_Connector, 0, Num_Connector.Length);
    }
}
