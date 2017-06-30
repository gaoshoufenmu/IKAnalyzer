using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKAnalyzer.core
{
    public class LexemePath : QuickSortSet<Lexeme>, IComparable<LexemePath>
    {
        /// <summary>
        /// 词元链的相对起始位置
        /// </summary>
        private int _begin;
        /// <summary>
        /// 词元链的相对起始位置
        /// </summary>
        public int Begin { get { return _begin; } }
        /// <summary>
        /// 词元链的相对截止位置
        /// </summary>
        private int _end;
        /// <summary>
        /// 词元链的相对截止位置
        /// </summary>
        public int End { get { return _end; } }
        /// <summary>
        /// 词元链的有效长度
        /// </summary>
        private int _length;
        /// <summary>
        /// 词元链的有效长度，在词元链插入了没有重叠的词元时可能与<seealso cref="PathSpan"/>不同（因为此时两个词元之间可能不是无缝连接）
        /// </summary>
        public int Length { get { return _length; } }
        /// <summary>
        /// 词元链首尾位置间的间隔（跨度）
        /// </summary>
        public int PathSpan { get { return _end - _begin; } }
        public LexemePath()
        {
            _begin = -1;
            _end = -1;
            _length = 0;
        }

        /// <summary>
        /// 使用与当前词元路径有重叠的词元来扩展
        /// </summary>
        /// <param name="l"></param>
        /// <returns>是否扩展成功</returns>
        public bool ExpandOverlapLexeme(Lexeme l)
        {
            if (Size == 0)       // 当前词元链还没有任何词元时，则直接插入参数给定词元
            {
                Insert(l);
                _begin = l.Begin;
                _end = l.Begin + l.Length;
                _length += l.Length;
                return true;
            }
            else if (CheckOverlap(l))
            {
                Insert(l);
                var l_end = l.Begin + l.Length;
                if (l_end > this._end)
                    this._end = l_end;

                this._length = this._end - this._begin;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 使用与当前词元路径没有重叠的词元来扩展
        /// </summary>
        /// <param name="l"></param>
        /// <returns></returns>
        public bool ExpandNonOverlapLexeme(Lexeme l)
        {
            if(Size == 0)       // 当前词元链还没有任何词元时，则直接插入参数给定词元
            {
                Insert(l);
                _begin = l.Begin;
                _end = l.Begin + l.Length;
                _length += l.Length;
                return true;
            }
            else if(!CheckOverlap(l))
            {
                Insert(l);
                _length += l.Length;
                _begin = PeekFirst().Begin;
                var tail = PeekLast();
                _end = tail.Begin + tail.Length;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 移除并返回结尾的词元
        /// </summary>
        /// <returns></returns>
        public Lexeme RemoveTail()
        {
            var tail = PollLast();
            if(Size == 0)
            {
                _begin = -1;
                _end = -1;
                _length = 0;
            }
            else
            {
                var newTail = PeekLast();
                _end = newTail.Begin + newTail.Length;
                
                // 由于tail 比 newTail 大，所有只可能有如下两种情况，
                //? 原来Java项目中直接使用 this._length -= tail.Length; 暂不清楚用意
                if(tail.Begin > newTail.Begin)
                {
                    if (tail.Begin >= this._end)                             // tail 与移除后的词元链没有重叠
                        _length -= tail.Length;
                    else
                        _length = newTail.Begin + newTail.Length - _begin;  // tail 与移除后的词元链有重叠
                }
                else
                {
                    // 此时满足： tail.Begin == newTail.Begin && tail.Length < newTail.Length;
                    // 故 移除 tail 对 this._end 没有影响
                }
            }
            return tail;
        }

        /// <summary>
        /// 检查与当前词元链是否有重叠（歧义的切分）
        /// </summary>
        /// <param name="l"></param>
        /// <returns></returns>
        public bool CheckOverlap(Lexeme l) =>
            (l.Begin >= this._begin && l.Begin < this._end) ||              // 词元头部与词元链尾部重叠
            (this._begin >= l.Begin && this._begin < l.Begin + l.Length);   // 词元尾部与词元链头部重叠

        /// <summary>
        /// X权重（词元长度积）
        /// </summary>
        /// <returns></returns>
        public int GetXWeight()
        {
            int prod = 1;
            var cur = Head;

            while(cur != null && cur.V != null)
            {
                prod *= cur.V.Length;
                cur = cur.Next;
            }
            return prod;
        }

        /// <summary>
        /// 词元位置权重
        /// </summary>
        /// <returns></returns>
        public int GetPWeight()
        {
            int pos_weight = 0;
            int pos = 0;

            var cur = Head;
            while(cur != null && cur.V != null)
            {
                pos++;
                pos_weight += pos * cur.V.Length;
                cur = cur.Next;
            }
            return pos_weight;
        }

        /// <summary>
        /// Copy 当前词元链
        /// </summary>
        /// <returns></returns>
        public LexemePath Copy()
        {
            var copy = new LexemePath() { _begin = this._begin, _end = this._end, _length = this.Length };
            var cur = Head;
            while(cur != null && cur.V != null)
            {
                copy.Insert(cur.V);
                cur = cur.Next;
            }
            return copy;
        }


        public override string ToString()
        {
            var sb = new StringBuilder(100);
            sb.Append("begin: ").Append(_begin).Append("\r\n").Append("end: ").Append(_end).Append("\r\n").Append("length: ").Append(_length).Append("\r\n");
            var cur = Head;
            while(cur != null)
            {
                sb.Append("lexeme: ").Append(cur.V).Append("\r\n");
                cur = cur.Next;
            }
            return sb.ToString();
        }

        public int CompareTo(LexemePath other)
        {
            if (this._length > other._length) return -1;    // 有效文本长度越长越好

            if (this._length < other._length) return 1;

            if (Size < other.Size) return -1;               // 词元数量越少越好

            if (Size > other.Size) return 1;

            if (PathSpan > other.PathSpan) return -1;       // 路径跨度越大越好

            if (PathSpan < other.PathSpan) return 1;

            if (this._end > other._end) return -1;          // 根据统计学结论，逆向切分概率高于正向切分，所以位置越靠后越好

            if (this._end < other._end) return 1;

            var x_weight_1 = this.GetXWeight();
            var x_weight_2 = other.GetXWeight();

            if (x_weight_1 > x_weight_2) return -1;      // 词元长度越平均越好

            if (x_weight_1 < x_weight_2) return 1;

            var p_weight_1 = this.GetPWeight();
            var p_weight_2 = other.GetPWeight();

            if (p_weight_1 > p_weight_2) return -1;     // 词元位置权重比较

            if (p_weight_1 < p_weight_2) return 1;

            return 0;
        }
    }
}
