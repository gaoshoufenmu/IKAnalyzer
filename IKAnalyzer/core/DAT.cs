using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace IKAnalyzer.core
{
    [Serializable]
    public class DAT<V> : IDisposable
    {
        [NonSerialized]
        public const int BUF_SIZE = 16384;
        /// <summary>
        /// base和check 两个数组中所占大小单位（两个int）
        /// </summary>
        [NonSerialized]
        public const int UNIT_SIZE = 8;

        /// <summary>
        /// check数组，表示回溯
        /// </summary>
        protected int[] _check;
        /// <summary>
        /// base数组，保存了当前节点node的在base数组中的offset
        /// 此offset加上node的输入字符c就得到对应子节点的在base中的index
        /// </summary>
        protected int[] _base;
        /// <summary>
        /// 确保base数组中的值唯一
        /// </summary>
        [NonSerialized] // 不需要序列化，除非需要动态添加节点
        private HashSet<int> _used;
        /// <summary>
        /// 数组的实际使用大小
        /// 用于查询时检测是否越界
        /// </summary>
        protected int _size;

        /// <summary>
        /// 每个key 的长度
        /// </summary>
        private int[] _length;
        /// <summary>
        /// key列表
        /// </summary>
        private List<string> _key;
        private int[] _value;
        /// <summary>
        /// 叶子节点保存的值
        /// </summary>
        private V[] _v;

        /// <summary>
        /// 数组的内存占用大小
        /// </summary>
        private int _allocSize;
        private int _nextCheckPos;
        private int _progress;
        /// <summary>
        /// 错误值
        /// </summary>
        private int _error;
        /// <summary>
        /// 叶子节点个数
        /// </summary>
        public int Size { get => _v == null ? 0 : _v.Length; }
        private class Node
        {
            /// <summary>
            /// 关键字符char的值+1
            /// 关键字符是siblings兄弟节点互相区分的字符，类似与普通TrieNode中的Map的char key
            /// </summary>
            public int code;
            public int depth;
            /// <summary>
            /// inclusive
            /// 当前节点在父节点的兄弟节点中的序号（按字典顺序排列，从0开始）
            /// </summary>
            public int left;
            /// <summary>
            /// 上一个兄弟节点的右位置是下一个兄弟节点的左位置，
            /// 最后一个兄弟节点的右位置是父节点的右位置
            /// exclusive
            /// </summary>
            public int right;

            public Node() { }
            public Node(int l, int d)
            {
                left = l;
                depth = d;
            }
            public Node(int l, int r, int d)
            {
                left = l;
                right = r;
                depth = d;
            }

            public override string ToString() => $"code: {code}, depth: {depth}, left: {left}, right: {right}";
        }

        public DAT()
        {
            _used = new HashSet<int>();
        }

        /// <summary>
        /// 比如Build文件头中的Trie时，keys为 “啊，阿胶，阿根廷，阿拉伯，阿拉伯人，埃及”
        /// </summary>
        /// <param name="keys">键列表</param>
        /// <param name="values">值</param>
        /// <returns>是否出错，出错则小于0</returns>
        public int Build(List<string> keys, List<V> values)
        {
            if (keys == null || values == null) throw new ArgumentNullException("keys or values are null");
            if (keys.Count != values.Count || keys.Count == 0) throw new ArgumentException("keys count isn't equal to values count or empty keys");

            _key = keys;
            _v = values.ToArray();
            Resize(65536 * 32);     // 首次建立Trie，初始化各数组
            _base[0] = 1;
            _nextCheckPos = 0;

            var root = new Node(0, keys.Count, 0);
            var siblings = FetchSiblings(root);
            Insert(siblings);

            _used = null;
            _key = null;
            _length = null;
            return _error;
        }

        public int Build(SortedDictionary<string, V> dict)
        {
            var keys = new List<string>(dict.Count);
            var values = new List<V>(dict.Count);
            foreach (var p in dict)
            {
                keys.Add(p.Key);
                values.Add(p.Value);
            }
            return Build(keys, values);
        }

        /// <summary>
        /// 获取给定父节点直接相连的子节点
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        private List<Node> FetchSiblings(Node parent)
        {
            if (_error < 0) return null;

            var siblings = new List<Node>();
            int prev = 0;
            for (var i = parent.left; i < parent.right; i++)
            {
                // 根据Trie的性质，每个边表示一个字符，那么key的长度就表示对应节点的depth，子节点的深度必然大于父节点的深度，根据这一点来过滤非parent节点的子孙节点
                // 需要过滤的原因是各节点的子孙节点覆盖在_base数组的范围可能会重叠（_check数组可以用来对重叠部分进行检测）
                if ((_length != null ? _length[i] : _key[i].Length) < parent.depth) continue;

                var tmp = _key[i];
                int cur = 0;
                if ((_length != null ? _length[i] : tmp.Length) != parent.depth)    // 如果不是parent节点
                    // 获取parent的子节点的关键字符（区分各子节点的字符，类似基于Map的Trie中的char key，比如文件头Trie，阿节点depth为1，其子节点的key在parent.depth出的字符分别为 根，胶，拉
                    // cur其实是字符char 的值 加 1，保证其对parent的base值偏移最小为1（对应'\0'字符），base值偏移为0的地方存储 parent节点自身作为终结词的信息
                    cur = tmp[parent.depth] + 1;

                if (prev > cur)      // 因为parent下各子节点是按字典顺序排列，所以如果出现prev>cur，则出错
                {
                    _error = 3;
                    return null;
                }

                // 1. siblings.Count = 0,表示是parent下根据字典顺序词条parent.depth处的（按字典排序的第一个）字符对应的节点添加到列表，比如“阿”节点下的第一个子节点“阿根”
                // 2. cur != prev 保证了去重复，比如“阿”节点下的“阿根”和“阿根廷”在parent.depth(=1)处的字符‘根’对应的节点“阿根”添加到siblings，而“阿根廷”则不能
                if (cur != prev || siblings.Count == 0)
                {
                    var tmp_node = new Node(i, parent.depth + 1);
                    tmp_node.code = cur;
                    if (siblings.Count != 0)
                        siblings[siblings.Count - 1].right = i;
                    siblings.Add(tmp_node);
                }
                prev = cur;
            }
            if (siblings.Count != 0)
                siblings[siblings.Count - 1].right = parent.right;
            return siblings;
        }

        private int Insert(List<Node> siblings)
        {
            if (_error < 0) return 0;

            int begin = 0;
            int nonzero_num = 0;
            int first = 0;

            // 这一批兄弟节点中某节点即将保存在数组中的位置position，首先考虑计划将第一个兄弟节点保存到数组的哪个位置
            // 通过比较上一次插入操作计算的下一次插入位置_nextCheckPos和根据当前节点字符编码值code（加1，以使得非root节点的check值大于等于1，不为0）的大小比较，选择一个较大值，
            // 这么做的原因是上一次插入操作中计算好了数组中不太小的连续的空闲范围的起始位置：_nextCheckPos，也就是说本次插入操作，节点起始位置必须不小于这个位置，否则数组中连续空闲位置不够
            // Max函数获取到插入第一个兄弟节点的数组计划位置，然后 -1 获取这个计划位置的前一个位置
            int pos = Math.Max(siblings[0].code + 1, _nextCheckPos) - 1;

            if (_allocSize <= pos)
                Resize(pos + 2);            // 这里一举进行Resize(pos+2)操作，原java代码中是Resize(pos+1)操作，但是个人觉得，如果满足了_alloSize <= pos，势必第一次进入while loop 还要执行一次Resize操作，
                                            // 不如这里干脆这么修改

            var lastNodeCode = siblings[siblings.Count - 1].code;   // 最后一个兄弟节点的码值，用于确定最后一个兄弟节点的保存位置

            outer:
            // 此循环体的目标是找出满足base[begin + a1...an] == 0的n个空闲空间,a1...an是siblings中的n个节点
            while (true)
            {
                pos++;
                if (_allocSize <= pos)
                    Resize(pos + 1);
                if (_check[pos] != 0)    // 寻找空闲位置
                {
                    nonzero_num++;
                    continue;
                }
                else if (first == 0)
                {
                    _nextCheckPos = pos;    // 下一次插入时必须在_nextCheckPos之后，由于所有的词条按字典顺序排序，所以这么做是有意义的
                    first = 1;
                }
                // 寻找到暂时满足空闲条件的一个位置

                begin = pos - siblings[0].code;     // 当前位置离第一个兄弟节点的距离

                // begin + lastNodeCode: 最后一个兄弟节点保存位置
                if (_allocSize <= (begin + lastNodeCode))      // 检测当前数组剩余容量是否能容下本次所有兄弟节点，具体是检测最后一个兄弟节点计划插入位置是否超出数组范围
                    Resize(begin + lastNodeCode + char.MaxValue);

                if (_used.Contains(begin)) continue;     // 这里额外增加了一个begin值唯一性保证，也就是说保证 base数组的值是唯一的，原因不清楚，有待进一步考究...

                for (var i = 1; i < siblings.Count; i++)
                    if (_check[begin + siblings[i].code] != 0)   // 如果不满足连续空闲位置条件，则跳至while循环重新开始寻找下一个计划开始保存的位置pos
                        goto outer;

                break;      // 如果满足以上条件，则表示找到实际开始保存的位置pos
            }

            if (1.0 * nonzero_num / (pos - _nextCheckPos + 1) >= 0.95)  // 如果建议的起始保存位置与实际保存位置之间至少有95%的空间被占用，则修改建议的起始保存位置，此位置之前的空间在下一次插入操作中不再被考虑
                _nextCheckPos = pos;

            _used.Add(begin);    // 设置 某个node的 base 值已被占用，任何其他节点的 base值 不能使用此值

            if (_size < begin + lastNodeCode + 1)   // 最后一个兄弟节点的保存位置为 begin+lastNodeCode，所以数组实际大小不低于 begin+lastNodeCode+1
                _size = begin + lastNodeCode + 1;

            for (var i = 0; i < siblings.Count; i++)
                _check[begin + siblings[i].code] = begin;

            for (var i = 0; i < siblings.Count; i++)
            {
                var new_siblings = FetchSiblings(siblings[i]);
                if (new_siblings == null)   // 出现错误
                    return 0;
                if (new_siblings.Count == 0)                 // 当前兄弟节点没有子节点，说明这个兄弟节点是叶节点，表示词结束
                {
                    _base[begin + siblings[i].code] = _value != null ? (-_value[siblings[i].left] - 1) : (-siblings[i].left - 1);
                    if (_value != null && (-_value[siblings[i].left] - 1) >= 0)      // 叶节点的base值必须为负，否则出错
                    {
                        _error = -2;
                        return 0;
                    }
                    _progress++;
                }
                else
                {   // 如果当前兄弟节点非叶节点
                    int h = Insert(new_siblings);   // dfs，深度优先
                    _base[begin + siblings[i].code] = h;    // 设置当前节点的base值
                }
            }
            return begin;       // 父节点的base值，父节点根据此值 加上输入字符c 的值，得到字符 c 对应的节点，类似于普通的TrieNode中的Map[c] -> TrieNode 操作
        }

        /// <summary>
        /// 拓展数组
        /// </summary>
        /// <param name="newSize"></param>
        /// <returns></returns>
        private int Resize(int newSize)
        {
            var base2 = new int[newSize];
            var check2 = new int[newSize];

            if (_allocSize > 0)
            {
                Array.Copy(_base, base2, _allocSize);
                Array.Copy(_check, check2, _allocSize);
            }
            _base = base2;
            _check = check2;

            return _allocSize = newSize;
        }

        /// <summary>
        /// 获取check中值非0的个数 => 非root节点的个数
        /// </summary>
        /// <returns></returns>
        public int GetNonzeroCount()
        {
            int res = 0;
            for (int i = 0; i < _check.Length; i++)
                if (_check[i] != 0)
                    ++res;
            return res;
        }
        public int ExactMatch(string key) => ExactMatch(key, 0, key.Length, 0);
        /// <summary>
        /// 精确匹配
        /// </summary>
        /// <param name="key">匹配串</param>
        /// <param name="start">匹配串的起始匹配位置</param>
        /// <param name="len">匹配串需要匹配的长度</param>
        /// <param name="nodePos">双数组查询的起始位置</param>
        /// <returns>_v列表下标</returns>
        public int ExactMatch(string key, int start, int len, int nodePos)
        {
            if (start < 0 || start > key.Length) start = 0;
            if (nodePos < 0 || nodePos >= _size) nodePos = 0;

            var end = start + len;
            if (len <= 0 || end > key.Length)
                end = key.Length;


            int res = -1;
            int p = 0;
            var b = _base[nodePos];     // 起始查询节点的base值（上文中的begin）

            for (var i = start; i < end; i++)
            {
                // 获取下一个节点的位置
                p = b + key[i] + 1;         // 注意每个节点的code值为 char 值 + 1，可以去FetchSiblings方法中查看

                // 查询时，根据一个字符码值转移后的位置p可能越界，所以需要检测 p < _size
                if (p < _size && b == _check[p])         // 如果检测成功，表示是合法的下一个节点
                    b = _base[p];           // 获取下一个节点的 base 值
                else
                    return res;
            }
            // 根据key的匹配字符(子)串的各字符序列，找到一个节点，下面查看该节点是否是叶节点
            p = b;      // 设置找到的目标节点的 base值 到 p 变量
            int n = _base[p];
            if (b == _check[p] && n < 0)
            {
                return -n - 1;
            }
            return res;
        }

        public List<int> PrefixMatch(string key) => PrefixMatch(key, 0, key.Length, 0);

        public List<int> PrefixMatch(string key, int start, int len, int nodePos)
        {
            if (start < 0 || start > key.Length) start = 0;
            if (nodePos < 0 || nodePos >= _size) nodePos = 0;

            var end = start + len;
            if (len <= 0 || end > key.Length)
                end = key.Length;

            var res = new List<int>();

            int b = _base[nodePos];
            int n;
            int p;

            for (var i = start; i < end; i++)
            {
                p = b + key[i] + 1;
                if (p < _size && b == _check[p])
                    b = _base[p];
                else
                    return res;

                p = b;
                n = _base[p];
                if (b == _check[p] && n < 0)
                    res.Add(-n - 1);
            }
            return res;
        }
        public List<Tuple<string, V>> PrefixMatchWithValue(char[] chars, int start, int len, int nodePos)
        {
            if (start < 0 || start > chars.Length) start = 0;
            if (nodePos < 0 || nodePos >= _size) nodePos = 0;

            var end = start + len;
            if (len <= 0 || end > chars.Length)
                end = chars.Length;

            var list = new List<Tuple<string, V>>();
            int b = _base[nodePos];
            int n;
            int p;

            for (int i = start; i < end; i++)
            {
                p = b;
                n = _base[p];
                if (b == _check[p] && n < 0)
                    list.Add(new Tuple<string, V>(new string(chars, start, i - start), _v[-n - 1]));

                p = b + chars[i] + 1;
                if (p < _size && b == _check[p])
                    b = _base[p];
                else
                    return list;
            }

            p = b;
            n = _base[p];
            if (b == _check[p] && n < 0)
                list.Add(new Tuple<string, V>(new string(chars, start, end - start), _v[-n - 1]));

            return list;
        }

        public List<Tuple<string, V>> PrefixMatchWithValue(string key, int start, int len, int nodePos) =>
            PrefixMatchWithValue(key.ToCharArray(), start, len, nodePos);

        public V[] Values { get => _v == null ? new V[0] : _v.ToArray(); }

        public V this[string key]
        {
            get
            {
                var index = ExactMatch(key);
                if (index >= 0)
                    return _v[index];

                throw new KeyNotFoundException("key does'n exist: " + key);
            }
            set
            {
                var index = ExactMatch(key);
                if (index >= 0)
                {
                    _v[index] = value;
                }
                else
                    throw new KeyNotFoundException();
            }
        }

        public bool Set(string key, V value)
        {
            int index = ExactMatch(key);
            if (index > 0)
            {
                _v[index] = value;
                return true;
            }
            return false;
        }

        public V GetOrDefault(string key, V v = default(V))
        {
            var index = ExactMatch(key);
            if (index >= 0)
                return _v[index];
            return v;
        }

        /// <summary>
        /// 使用V值数组的下标获取V值
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public V GetByIndex(int index)
        {
            if (index >= 0 && index < _v.Length)
                return _v[index];
            return default(V);
        }

        public bool ContainsKey(string key) => ExactMatch(key) >= 0;

        /// <summary>
        /// 根据路径进行状态转移
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="nodePos">Trie起始匹配节点位置</param>
        /// <returns>最后一个状态的数组下标</returns>
        public int Transition(string path, int nodePos = 0)
        {
            if (nodePos < 0) nodePos = 0;

            var b = _base[nodePos];
            int p;
            for (int i = 0; i < path.Length; i++)
            {
                p = b + path[i] + 1;
                if (p < _size && b == _check[p])
                    b = _base[p];
                return -1;
            }
            p = b;
            return p;
        }
        /// <summary>
        /// 根据一个输入字符进行状态转移
        /// </summary>
        /// <param name="c">输入字符</param>
        /// <param name="nodePos">Trie起始匹配节点位置</param>
        /// <returns>转移后状态的数组下标</returns>
        public int Transition(char c, int nodePos = 0)
        {
            if (nodePos < 0) nodePos = 0;

            var b = _base[nodePos];
            int p = b + c + 1;
            if (p < _size && b == _check[p])
                return _base[p];

            return -1;
        }
        /// <summary>
        /// 输出某一个状态的值
        /// </summary>
        /// <param name="state">某一状态(数组下标)</param>
        /// <returns>此状态的值</returns>
        public V GetValueOrDefault(int state, V v = default(V))
        {
            if (state < 0 || state >= _size) return v;

            int n = _base[state];
            if (state == _check[state] && n < 0)
                return _v[-n - 1];

            return v;
        }

        public bool Serialize(string path)
        {
            var fs = new FileStream(path, FileMode.Create);
            try
            {
                var bf = new BinaryFormatter();
                bf.Serialize(fs, this);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
            finally
            {
                fs.Close();
            }
        }

        public static DAT<V> Deserialize(string path)
        {
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            try
            {
                var bf = new BinaryFormatter();
                var trie = bf.Deserialize(fs) as DAT<V>;
                return trie;
            }
            catch (Exception e)
            {
                return null;
            }
            finally
            {
                fs.Close();
            }
        }

        public bool SaveDoubleArray(string path)
        {
            var fs = new FileStream(path, FileMode.Create);

            var res = Save(fs);
            fs.Close();

            return res;
        }

        /// <summary>
        /// 保存key的相关信息：base和check双数组
        /// </summary>
        /// <param name="fs"></param>
        /// <returns></returns>
        public bool Save(FileStream fs)
        {
            try
            {
                var bytes = BitConverter.GetBytes(_size);
                fs.Write(bytes, 0, 4);

                for (int i = 0; i < _size; i++)
                {
                    bytes = BitConverter.GetBytes(_base[i]);
                    fs.Write(bytes, 0, 4);
                    bytes = BitConverter.GetBytes(_check[i]);
                    fs.Write(bytes, 0, 4);
                }
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }

        public bool LoadDoubleArray(string path)
        {
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            try
            {
                var bytes = new byte[4];
                fs.Read(bytes, 0, 4);
                _size = BitConverter.ToInt32(bytes, 0);

                _base = new int[_size];
                _check = new int[_size];
                for (int i = 0; i < _size; i++)
                {
                    fs.Read(bytes, 0, 4);
                    _base[i] = BitConverter.ToInt32(bytes, 0);
                    fs.Read(bytes, 0, 4);
                    _check[i] = BitConverter.ToInt32(bytes, 0);
                }
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
            finally
            {
                fs.Close();
            }
        }

        public bool Load(string path, V[] values) => Load(ByteArray.Create(path), values);

        public bool Load(ByteArray ba, V[] values)
        {
            if (ba == null) return false;

            _size = ba.NextInt();
            _base = new int[_size + 65536];     // 多留一些空间，防止越界
            _check = new int[_size + 65536];

            for (int i = 0; i < _size; i++)
            {
                _base[i] = ba.NextInt();
                _check[i] = ba.NextInt();
            }
            _v = values;
            _used = null;   // 无用的对象，可以释放
            return true;
        }

        public void Dispose()
        {
            _check = null;
            _base = null;
            _used = null;
            _size = 0;
            _allocSize = 0;
        }

        public Searcher GetSearcher(string text, int offset) => new Searcher(offset, text.ToCharArray(), this);
        public Searcher GetSearcher(char[] chars, int offset) => new Searcher(offset, chars, this);

        public class Searcher
        {
            /// <summary>
            /// 某一轮开始匹配的位置
            /// </summary>
            public int begin;
            /// <summary>
            /// 匹配成功时匹配到的key长度
            /// </summary>
            public int length;
            /// <summary>
            /// 在原始列表中的下标
            /// </summary>
            public int index;
            /// <summary>
            /// 对应的值，根据<seealso cref="index"/>获取
            /// </summary>
            public V value;
            /// <summary>
            /// 传入的字符数组
            /// </summary>
            private char[] charArr;
            /// <summary>
            /// 上一个node在base数组中的位置
            /// 用于贪婪匹配，即匹配成功后，下轮匹配还是在老的一轮上继续匹配
            /// </summary>
            private int last;
            /// <summary>
            /// 上一个字符的下标
            /// </summary>
            private int i;

            private DAT<V> _dat;

            public Searcher(int offset, char[] chars, DAT<V> dat)
            {
                _dat = dat;
                charArr = chars;
                i = offset;
                last = dat._base[0];
                if (chars.Length == 0)
                    begin = -1;
                else
                    begin = offset;
            }

            public bool Next()
            {
                int b = last;
                int n;
                int p;

                for (; ; i++)
                {
                    if (i == charArr.Length)             // 当前处理字符位置已经到头，没有匹配到一个输出，则将begin位置后移一位，重新开始匹配
                    {
                        ++begin;
                        if (begin == charArr.Length) break;     // 匹配结束，退出

                        i = begin;                      // 当前处理字符位置从begin开始，开始新一轮匹配
                        b = _dat._base[0];              // 状态重置
                    }

                    p = b + charArr[i] + 1;     // 状态转移 p = base[char[i-1]] + char[i] + 1

                    if (b == _dat._check[p])
                        b = _dat._base[p];      // 转移成功
                    else
                    {                           // 转移失败
                        i = begin;              // 当前处理字符位置回退到本轮开始匹配的位置，由for循环将i后移一位开启新一轮匹配
                        ++begin;                // 当前开始处理的位置向后移一位
                        if (begin == charArr.Length)     // 匹配结束，退出
                            break;

                        b = _dat._base[0];      // 状态重置
                        continue;
                    }

                    // 检测是否有输出
                    p = b;
                    n = _dat._base[p];
                    if (b == _dat._check[p] && n < 0)
                    {
                        // 有输出
                        length = i - begin + 1;     // i: 当前处理字符，begin：开始处理字符
                        index = -n - 1;
                        value = _dat._v[index];
                        last = b;                   // 记录上一个匹配在base数组中的位置
                        ++i;
                        return true;
                    }

                    // 到这里，表示转移一直成功，继续循环匹配
                }
                return false;
            }
        }
    }
}
