using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKAnalyzer.core
{
    /// <summary>
    /// IK 词元对象
    /// </summary>
    public class Lexeme : IComparable<Lexeme>
    {
        /// <summary>
        /// 未知
        /// </summary>
        public const int TYPE_UNKNOWN = 0;
        /// <summary>
        /// 英文
        /// </summary>
        public const int TYPE_ENGLISH = 1;
        /// <summary>
        /// 阿拉伯数字
        /// </summary>
        public const int TYPE_ARABIC = 2;
        /// <summary>
        /// 英文数字混合
        /// </summary>
        public const int TYPE_ALPHANUM = 3;
        /// <summary>
        /// 中文词元
        /// </summary>
        public const int TYPE_CN_WORD = 4;
        /// <summary>
        /// 中文单字
        /// </summary>
        public const int TYPE_CN_CHAR = 64;
        /// <summary>
        /// 中日韩
        /// </summary>
        public const int TYPE_OTHER_CJK = 8;
        /// <summary>
        /// 中文数词
        /// </summary>
        public const int TYPE_CN_NUM = 16;
        /// <summary>
        /// 中文量词
        /// </summary>
        public const int TYPE_CN_QUANT = 32;
        /// <summary>
        /// 中文数量词
        /// </summary>
        public const int TYPE_CN_NUM_QUANT = 48;

        /// <summary>
        /// 词元起始位移
        /// </summary>
        private int _offset;
        /// <summary>
        /// 词元的相对起始位置
        /// </summary>
        private int _begin;
        /// <summary>
        /// 词元长度
        /// </summary>
        private int _length;
        /// <summary>
        /// 词元文本
        /// </summary>
        private string _text;
        /// <summary>
        /// 词元类型
        /// </summary>
        private int _type;
        /// <summary>
        /// 词元起始位移
        /// </summary>
        public int Offset { get { return _offset; } }
        /// <summary>
        /// 词元的相对起始位置
        /// </summary>
        public int Begin
        {
            get { return _begin; }
            set { _begin = value; }
        }
        
        /// <summary>
        /// 词元长度
        /// </summary>
        public int Length
        {
            get { return _length; }
            set
            {
                if (value < 0)
                    throw new Exception("length of lexeme can not be little than 0");
                _length = value;
            }
        }
        /// <summary>
        /// 词元文本
        /// </summary>
        public string Text
        {
            get { return _text; }
            set
            {
                if (value == null)
                {
                    _text = "";
                }
                else
                    _text = value;

                _length = _text.Length;
            }
        }
        /// <summary>
        /// 词元类型
        /// </summary>
        public int Type
        {
            get { return _type; }
            set { _type = value; }
        }
        /// <summary>
        /// 词元的（绝对）开始位置(inclusive)
        /// </summary>
        public int Start { get { return _offset + _begin; } }
        /// <summary>
        /// 词元的（绝对）结束位置(exclusive)
        /// </summary>
        public int Stop { get { return _offset + _begin + _length; } }

        public Lexeme(int offset, int begin, int length, int type)
        {
            if (_length < 0) throw new ArgumentException("length of lexeme can not be little than 0");

            _offset = offset;
            _begin = begin;
            _length = length;
            _type = type;
        }

        /// <summary>
        /// 比较两词元是否相等（同）
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (this == obj) return true;

            var o = obj as Lexeme;
            if (o == null) return false;

            return this._offset == o._offset && this._begin == o._begin && this._length == o._length;
        }
        /// <summary>
        /// 计算词元哈希
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            if (_length == 0) return 0;

            var start = Start;
            var stop = Stop;

            return start * 37 + stop * 31 + ((start * stop) % _length) * 11;
        }

        /// <summary>
        /// 词元在排序集合中比较算法
        /// 相对起始位置越靠前的词元越小，而相对起始位置相同时，长度越大的词元越小
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(Lexeme other)
        {
            if (this._begin < other._begin)
                return -1;
            else if (this._begin > other._begin)
                return 1;
            else
            {
                if (this._length > other._length)
                    return -1;
                else if (this._length < other._length)
                    return 1;
                return 0;
            }
        }

        /// <summary>
        /// 将给定词元并入当前词元
        /// </summary>
        /// <param name="l">给定词元参数</param>
        /// <param name="type">合并之后的词元类型</param>
        /// <returns>是否合并成功</returns>
        public bool Expand(Lexeme l, int type)
        {
            if(l != null && this.Stop == l.Start)   // 可以合并的条件
            {
                _length += l._length;
                _type = type;
                return true;
            }
            return false;
        }
        /// <summary>
        /// 获取词元类型的字符串形式
        /// </summary>
        /// <returns></returns>
        public string GetTypeString()
        {
            switch(_type)
            {
                case TYPE_ENGLISH:
                    return "ENGLISH";
                case TYPE_ARABIC:
                    return "ARABIC";
                case TYPE_ALPHANUM:
                    return "ALPHANUM";
                case TYPE_CN_WORD:
                    return "CN_WORD";
                case TYPE_CN_CHAR:
                    return "CN_CHAR";
                case TYPE_OTHER_CJK:
                    return "OTHER_CJK";
                case TYPE_CN_NUM:
                    return "CN_NUM";
                case TYPE_CN_QUANT:
                    return "CN_QUANT";
                case TYPE_CN_NUM_QUANT:
                    return "CN_NUM_QUANT";
                default:
                    return "UNKNOWN";
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder(20);
            sb.Append(Start).Append("-").Append(Stop).Append(" : ").Append(_text).Append(" : \t").Append(GetTypeString());
            return sb.ToString();
        }
    }
}
