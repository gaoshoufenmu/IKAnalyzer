using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using IKAnalyzer.cfg;

namespace IKAnalyzer.core
{
    /// <summary>
    /// 分词上下文
    /// </summary>
    public class AnalyzeContext
    {
        /// <summary>
        /// 默认缓冲区大小
        /// </summary>
        private const int BUFF_SIZE = 4096;
        /// <summary>
        /// 缓冲区耗尽的临界值
        /// </summary>
        private const int BUFF_EXHAUST_CRITICAL = 100;

        /// <summary>
        /// 字符缓冲
        /// </summary>
        private char[] _charBuff;
        public char[] CharBuff { get { return _charBuff; } }
        /// <summary>
        /// 字符类型
        /// </summary>
        private int[] _charTypes;



        private int _buffOffset;
        public int BuffOffset { get { return _buffOffset; } }
        private int _cursor;
        public int Cursor { get { return _cursor; } }
        private int _buffSize;

        private HashSet<string> _buffLocker;
        /// <summary>
        /// 原始分词词元结果，未经歧义处理
        /// </summary>
        private QuickSortSet<Lexeme> _rawLexemes;
        /// <summary>
        /// 原始分词词元结果，未经歧义处理
        /// </summary>
        public QuickSortSet<Lexeme> RawLexemes { get => _rawLexemes; }
        /// <summary>
        /// 位置索引表
        /// </summary>
        private Dictionary<int, LexemePath> _pathDict;
        /// <summary>
        /// 最终分词结果集
        /// </summary>
        private List<Lexeme> _results;

        private IConfig _cfg;

        public int CurrentCharType { get => _charTypes[_cursor]; }
        public char CurrentChar { get => _charBuff[_cursor]; }

        public AnalyzeContext(IConfig cfg)
        {
            _cfg = cfg;
            _charBuff = new char[BUFF_SIZE];
            _charTypes = new int[BUFF_SIZE];
            _buffLocker = new HashSet<string>();
            _rawLexemes = new QuickSortSet<Lexeme>();
            _pathDict = new Dictionary<int, LexemePath>();
            _results = new List<Lexeme>();
        }

        /// <summary>
        /// 填充buff
        /// 首次填充，或者是buff中内容已经被快被使用完的时候再次填充buff
        /// </summary>
        /// <param name="sr"></param>
        /// <returns></returns>
        public int FillBuff(StreamReader sr)
        {
            int readCount = 0;                                  // 本次填充buff后，buff的有效内容长度
            if (_buffOffset == 0)                               // 首次读取reader
                readCount = sr.Read(_charBuff, 0, BUFF_SIZE);
            else                                                // cursor进入临界区，buff中内容快被使用完
            {
                int residueLen = _buffSize - 1 - _cursor;           // 剩余未处理内容长度
                if(residueLen > 0)                                  // 指针_cursor 到 _buffSize之间 [_cursor, _buffSize)尚未处理
                {
                    // 将尚未处理的部分移到buff头部，然后再将新内容读入buff
                    Array.Copy(_charBuff, Cursor, _charBuff, 0, residueLen);
                    readCount = residueLen;
                }
                readCount += sr.Read(_charBuff, residueLen, BUFF_SIZE - residueLen);
            }

            _buffSize = readCount;
            _cursor = 0;
            return readCount;
        }

        /// <summary>
        /// 初始化buff指针，并处理第一个字符
        /// </summary>
        public void InitCursor()
        {
            _cursor = 0;
            Normalize();
        }

        /// <summary>
        /// 规范化buff中当前指针处的字符
        /// </summary>
        private void Normalize()
        {
            _charBuff[_cursor] = CharUtil.Normalize(_charBuff[_cursor]);
            _charTypes[_cursor] = CharUtil.GetCharType(_charBuff[_cursor]);
        }

        /// <summary>
        /// 向后移动buff指针
        /// </summary>
        /// <returns>移动成功返回true，否则返回false</returns>
        public bool MoveCursor()
        {
            if(_cursor < _buffSize - 1)        // 指针位置小于最后一个elem位置，则可以移动指针
            {
                _cursor++;
                Normalize();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 设置当前buff为锁定状态
        /// 加入占用buff的分词器名称，表示分词器占用了buff
        /// </summary>
        /// <param name="segmenterName"></param>
        public void LockBuffer(string segmenterName) => _buffLocker.Add(segmenterName);
        /// <summary>
        /// 移除分词器名称，释放分词器对buff的占用
        /// </summary>
        /// <param name="segmenterName"></param>
        public void UnlockBuffer(string segmenterName) => _buffLocker.Remove(segmenterName);
        /// <summary>
        /// buff是否被占用
        /// </summary>
        /// <returns></returns>
        public bool IsBuffLocked() => _buffLocker.Count > 0;
        /// <summary>
        /// buff是否用完
        /// </summary>
        /// <returns></returns>
        public bool IsBuffConsumed() => _cursor == _buffSize - 1;

        /// <summary>
        /// 判断buff是否需要读取新数据
        /// </summary>
        /// <returns></returns>
        public bool NeedRefillBuff() =>
            _buffSize == BUFF_SIZE &&                           // buff满载，如果不满载，说明StreamReader已经读取完毕
            _cursor < _buffSize - 1 &&                          // buff指针位于临界区
            _cursor > _buffSize - BUFF_EXHAUST_CRITICAL &&
            !IsBuffLocked();                                    // 当前没有分词器占用buff

        /// <summary>
        /// 更新buff的起始位置
        /// </summary>
        public void UpdateOffset() => _buffOffset += _cursor;

        /// <summary>
        /// 添加词元到原始词元集合中
        /// </summary>
        /// <param name="l"></param>
        public void AddLexeme(Lexeme l) => _rawLexemes.Insert(l);

        /// <summary>
        /// 添加分词结果路径
        /// key -> 路径起始位置，value -> 路径
        /// </summary>
        /// <param name="path"></param>
        public void AddLexemePath(LexemePath path)
        {
            if (path != null)
                _pathDict.Add(path.Begin, path);
        }

        public void OutputResults()
        {
            int index = 0;
            for(; index <= _cursor; )   // 对buff中已经处理过的部分内容，范围为[0, _cursor]，找出对应位置所有的词元路径
            {
                if(_charTypes[index] == CharUtil.CHAR_USELESS)  // 对应位置的字符如果是无效字符，则跳过
                {
                    index++;
                    continue;
                }

                // 以当前位置index为起始位置 begin 的词元路径
                if(_pathDict.TryGetValue(index, out var path))
                {
                    var lex = path.PollFirst();     // 获取首个词元
                    while(lex != null)
                    {
                        _results.Add(lex);
                        index = lex.Begin + lex.Length;
                        lex = path.PollFirst();
                        if(lex != null)
                        {
                            for (; index < lex.Begin; index++)
                                OutputSingleCJK(index);
                        }
                    }
                }
                else    // 当前位置为起始位置不存在词元路径，则将当前位置的字符作为单字符加入结果集
                {
                    OutputSingleCJK(index);
                    index++;
                }
            }
            _pathDict.Clear();
        }

        private void OutputSingleCJK(int index)
        {
            var lex_type = _charTypes[index] == CharUtil.CHAR_CHINESE ? Lexeme.TYPE_CN_CHAR : Lexeme.TYPE_OTHER_CJK;
            _results.Add(new Lexeme(_buffOffset, index, 1, lex_type));
        }

        /// <summary>
        /// 获取下一个词元，并将其从结果集中移除
        /// 适当的时候会将两个相邻词元合并（比如数量词合并）
        /// </summary>
        /// <returns></returns>
        public Lexeme GetNextLexeme()
        {
            var lex = _results.FirstOrDefault();
            if(lex != null)
                _results.RemoveAt(0);
            while (lex != null)
            {
                // 尝试数量词合并
                Compound(lex);
                // 如果lex 是停用词，则继续去结果集中的下一个词元
                if(DictTrie.Instance.IsStopword(_charBuff, lex.Begin, lex.Length))
                {
                    lex = _results.FirstOrDefault();
                    if (lex != null)
                        _results.RemoveAt(0);
                }
                else
                {
                    lex.Text = new string(_charBuff, lex.Begin, lex.Length);
                    break;
                }
            }
            return lex;
        }

        /// <summary>
        /// 组合词元
        /// </summary>
        /// <param name="lex"></param>
        public void Compound(Lexeme lex)
        {
            if (!_cfg.UseSmart) return;     // 没有启用智能分词，则直接返回，不进行组合

            if(_results.Count > 0)
            {
                if(lex.Type == Lexeme.TYPE_ARABIC)  // 如果是数词 （英文数词）
                {
                    var next = _results.FirstOrDefault();       // 前面做过检查，所以这里 next != null
                    bool append = false;

                    if (next.Type == Lexeme.TYPE_CN_NUM)                        // 下一个词元是中文数词
                        append = lex.Expand(next, Lexeme.TYPE_CN_NUM);
                    else if (next.Type == Lexeme.TYPE_CN_QUANT)                   // 下一个词元是中文量词
                        append = lex.Expand(next, Lexeme.TYPE_CN_NUM_QUANT);

                    if (append)
                        _results.RemoveAt(0);
                }

                // 可能存在第二轮合并
                if (_results.Count > 0 && lex.Type == Lexeme.TYPE_CN_NUM)
                {
                    var next = _results.FirstOrDefault();
                    bool append = false;
                    if (next.Type == Lexeme.TYPE_CN_QUANT)
                        append = lex.Expand(next, Lexeme.TYPE_CN_NUM_QUANT);
                    if (append)
                        _results.RemoveAt(0);
                }
            } 
        }

        public void Reset()
        {
            _buffLocker.Clear();
            _rawLexemes = new QuickSortSet<Lexeme>();
            _buffSize = 0;
            _buffOffset = 0;
            _charTypes = new int[BUFF_SIZE];
            _charBuff = new char[BUFF_SIZE];
            _pathDict.Clear();
            _results.Clear();
            _cursor = 0;
        }
    }
}
