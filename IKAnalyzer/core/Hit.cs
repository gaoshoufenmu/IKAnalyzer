using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKAnalyzer.core
{
    public class Hit
    {
        /// <summary>
        /// Hit 不匹配
        /// </summary>
        private const int UNMATCH = 0x00000000;
        /// <summary>
        /// Hit完全匹配
        /// </summary>
        private const int MATCH = 0x00000001;
        /// <summary>
        /// Hit前缀匹配
        /// </summary>
        private const int PREFIX = 0x00000010;
        /// <summary>
        /// Hit当前状态，默认不匹配
        /// </summary>
        private int _hitState = UNMATCH;

        public Node MatchedNode { get; set; }

        public int Begin { get; set; }
        public int End { get; set; }

        /// <summary>
        /// 是否完全匹配
        /// </summary>
        public bool IsMatch { get => (_hitState & MATCH) > 0; }
        /// <summary>
        /// 设置hit为匹配状态
        /// 注意：MATCH | PREFIX 表示既是完全匹配，又是前缀匹配，比如 “法克油”既可以完全匹配“法克”，也可以前缀匹配“法克”
        /// </summary>
        public void SetMatch() => _hitState = _hitState | MATCH;

        /// <summary>
        /// 判断是否是前缀匹配
        /// </summary>
        public bool IsPrefix { get => (_hitState & PREFIX) > 0; }
        public void SetPrefix() => _hitState = _hitState | PREFIX;
        public bool IsUnmatch { get => _hitState == UNMATCH; }
        public void SetUnmatch() => _hitState = UNMATCH;


    }
}
