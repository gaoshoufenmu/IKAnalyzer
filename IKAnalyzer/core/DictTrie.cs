using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using IKAnalyzer.cfg;
namespace IKAnalyzer.core
{
    /// <summary>
    /// 词典Trie树
    /// </summary>
    public class DictTrie
    {
        private static DictTrie _instance;
        public static DictTrie Instance { get => _instance; }
        /// <summary>
        /// 加载词典时的锁对象，保证线程安全
        /// </summary>
        private static object _locker = new object();
        /// <summary>
        /// 主词典
        /// </summary>
        private Node _mainDict;
        /// <summary>
        /// 停用词词典
        /// </summary>
        private Node _stopwordDict;
        /// <summary>
        /// 量词词典
        /// </summary>
        private Node _quantDict;

        private IConfig _cfg;

        private DictTrie(IConfig cfg)
        {
            _cfg = cfg;
            LoadMainDict();
            LoadStopwordDict();
            LoadQuantDict();
        }
        

        /// <summary>
        /// 由于加载词典需要较长的时间，所以提供一个手动控制加载词典的方法，
        /// 而不是由.net framework 自动加载静态对象
        /// </summary>
        /// <param name="cfg"></param>
        public static void Init(IConfig cfg)
        {
            if(_instance == null)
            {
                lock(_locker)
                {
                    if(_instance == null)
                    {
                        _instance = new DictTrie(cfg);
                    }
                }
            }
        }

        public void AddWords2MainDict(List<string> words)
        {
            for(int i = 0; i < words.Count; i++)
            {
                var word = words[i].Trim().ToLower();
                if (word != "") _mainDict.AddNode(word.ToCharArray());
            }
        }
        public void RemoveFromMainDict(List<string> words)
        {
            for(int i = 0; i < words.Count; i++)
            {
                var word = words[i].Trim().ToLower();
                if (word != "") _mainDict.DisableNode(word.ToCharArray());
            }
        }



        public Hit MatchWitMainDict(char[] chars) => _mainDict.Match(chars);
        public Hit MatchWithMainDict(char[] chars, int begin, int length) => _mainDict.Match(chars, begin, length);

        public Hit MatchWithQuantDict(char[] chars, int begin, int length) => _quantDict.Match(chars, begin, length);

        /// <summary>
        /// 从已匹配的Hit中获取前缀匹配的Node，并继续向后匹配一个字符
        /// </summary>
        /// <param name="chars">待匹配字符数组</param>
        /// <param name="curIndex">当前前缀匹配部分的最后一个字符位置</param>
        /// <param name="matchedHit">能前缀匹配的Trie树节点</param>
        /// <returns></returns>
        public Hit MatchWithHit(char[] chars, int curIndex, Hit matchedHit)
        {
            var node = matchedHit.MatchedNode;
            return node.Match(chars, curIndex, 1, matchedHit);
        }

        /// <summary>
        /// 是否是停用词
        /// </summary>
        /// <param name="chars"></param>
        /// <param name="begin"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public bool IsStopword(char[] chars, int begin, int length) => _stopwordDict.Match(chars, begin, length).IsMatch;

        private void LoadMainDict()
        {
            _mainDict = new Node('\0');
            //! --------------- 由于这里BufferedStream没有 ReadLine 方法，故直接采用File.ReadAllLines方法  -------------------
            //var fs = new FileStream(_cfg.MainDictPath, FileMode.Open, FileAccess.Read);
            //var bs = new BufferedStream(fs, 512);

            //
            var lines = File.ReadAllLines(_cfg.MainDictPath, Encoding.UTF8);
            for(int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim().ToLower();
                if (line == "") continue;

                _mainDict.AddNode(line.ToCharArray());
            }

            LoadExtDict();
        }

        private void LoadExtDict()
        {
            var extPaths = _cfg.ExtDictPaths;
            if (extPaths == null || extPaths.Count == 0) return;

            foreach(var path in extPaths)
            {
                var lines = File.ReadAllLines(path, Encoding.UTF8);
                for(int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim().ToLower();
                    if (line == "") continue;

                    _mainDict.AddNode(line.ToCharArray());
                }
            }
        }

        private void LoadStopwordDict()
        {
            _stopwordDict = new Node('\0');
            var paths = _cfg.ExtStopwordDictPaths;
            foreach(var path in paths)
            {
                var lines = File.ReadAllLines(path, Encoding.UTF8);
                for(int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim().ToLower();
                    if (line == "") continue;

                    _stopwordDict.AddNode(line.ToCharArray());
                }
            }
        }

        private void LoadQuantDict()
        {
            _quantDict = new Node('\0');
            var lines = File.ReadAllLines(_cfg.QuantDictPath, Encoding.UTF8);
            for(int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim().ToLower();
                if (line == "") continue;

                _quantDict.AddNode(line.ToCharArray());
            }
        }
    }

    public class Node
    {
        /// <summary>
        /// 数组大小上限
        /// </summary>
        private const int ARRAY_LENGTH_LIMIT = 3;

        // 子节点数量不超过此上限时使用数组存储，否则使用字典类型存储，频繁访问时，数组访问效率较高
        private Dictionary<char, Node> _childrenDict;
        private Node[] _childrenArray;

        private bool _threadSafe;
        /// <summary>
        /// 当前节点上的锁对象，用于多线程填充词典节点
        /// </summary>
        private object _locker;

        /// <summary>
        /// 节点上存储的字符
        /// </summary>
        private char _char;
        /// <summary>
        /// 子节点的实际数量
        /// </summary>
        private int _size;
        /// <summary>
        /// 节点状态
        /// 1 -> 从根结点到当前节点的路径表示一个词，0 -> 不是一个词
        /// </summary>
        private int _state;

        /// <summary>
        /// 是否有下一个节点（子节点）
        /// </summary>
        /// <returns></returns>
        public bool HasNext { get => _size > 0; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="char">当前节点关联的字符</param>
        /// <param name="threadSafe">是否需要保证线程安全</param>
        public Node(char @char, bool threadSafe = false)
        {
            _char = @char;
            _threadSafe = threadSafe;
            if (threadSafe)
                _locker = new object();
        }

        /// <summary>
        /// 获取子节点数组
        /// </summary>
        public Node[] ChildrenArray
        {
            get
            {
                if (_childrenArray == null)
                {
                    if (_threadSafe)
                    {
                        lock (_locker)
                        {
                            if (_childrenArray == null)
                                _childrenArray = new Node[ARRAY_LENGTH_LIMIT];
                        }
                    }
                    else
                    {
                        if (_childrenArray == null)
                            _childrenArray = new Node[ARRAY_LENGTH_LIMIT];
                    }
                }
                return _childrenArray;
            }
        }

        /// <summary>
        /// 获取子节点映射
        /// </summary>
        public Dictionary<char, Node> ChildrenDict
        {
            get
            {
                if (_childrenDict == null)
                {
                    if (_threadSafe)
                    {
                        lock (_locker)
                        {
                            if (_childrenDict == null)
                                _childrenDict = new Dictionary<char, Node>(ARRAY_LENGTH_LIMIT << 1);
                        }
                    }
                    else
                    {
                        if (_childrenDict == null)
                            _childrenDict = new Dictionary<char, Node>(ARRAY_LENGTH_LIMIT << 1);
                    }
                }
                return _childrenDict;
            }
        }

        /// <summary>
        /// 匹配
        /// </summary>
        /// <param name="chars">待匹配的字符数组</param>
        /// <returns></returns>
        public Hit Match(char[] chars) => Match(chars, 0, chars.Length, null);
        /// <summary>
        /// 匹配
        /// </summary>
        /// <param name="chars">待匹配的字符数组</param>
        /// <param name="begin">匹配起始位置</param>
        /// <param name="length">待匹配长度</param>
        /// <returns>匹配结果</returns>
        public Hit Match(char[] chars, int begin, int length) => Match(chars, begin, length, null);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="chars">待匹配的字符数组</param>
        /// <param name="begin">匹配起始位置</param>
        /// <param name="length">待匹配长度</param>
        /// <param name="searchHit">匹配结果对象</param>
        /// <returns></returns>
        public Hit Match(char[] chars, int begin, int length, Hit searchHit)
        {
            if (searchHit == null)
                searchHit = new Hit() { Begin = begin };    // 创建对象，并设置其实匹配位置
            else
                searchHit.SetUnmatch();     // 重置为初始默认状态

            var c = chars[begin];           // 当前需要匹配的字符

            searchHit.End = begin;          // 更新截止匹配位置

            var array = _childrenArray;
            var map = _childrenDict;

            Node node = null;
            if(array != null)
                node = BinarySearch(c, array, 0, _size);
            else if(map != null && map.ContainsKey(c))
                node = map[c];
            
            if(node != null)
            {
                if (length > 1)
                    return node.Match(chars, begin + 1, length - 1, searchHit);
                else if(length == 1)
                {
                    if (node._state == 1)
                        searchHit.SetMatch();
                    if (node.HasNext)
                    {
                        searchHit.SetPrefix();
                        searchHit.MatchedNode = node;
                    }
                    return searchHit;
                }
            }
            return searchHit;
        }

        /// <summary>
        /// 根据一个字符数组所指示的路径，添加节点
        /// </summary>
        /// <param name="chars"></param>
        public void AddNode(char[] chars) => AddNode(chars, 0, chars.Length, 1);
        /// <summary>
        /// 根据一个字符数组所指示的路径，屏蔽节点
        /// </summary>
        /// <param name="chars"></param>
        public void DisableNode(char[] chars) => AddNode(chars, 0, chars.Length, 0);

        /// <summary>
        /// 根据一个字符数组为路径，依次添加节点
        /// </summary>
        /// <param name="chars"></param>
        /// <param name="begin">字符数组中当前位置的字符将被作为新节点插入</param>
        /// <param name="length">从begin位置开始还有多少字符需要被处理</param>
        /// <param name="enabled">使能开关，加载新词时为1，屏蔽词时为0</param>
        /// <returns></returns>
        private bool AddNode(char[] chars, int begin, int length, int enabled)
        {
            var c = chars[begin];
            var node = GetNode(c, enabled);
            if (node != null)
            {
                if (length > 1)
                    node.AddNode(chars, begin + 1, length - 1, enabled);
                else if (length == 1)
                    node._state = enabled;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 给定下一个字符，从当前节点中获取对应的子节点
        /// </summary>
        /// <param name="c"></param>
        /// <param name="create">1 -> 如果子节点不存在，则创建</param>
        /// <returns></returns>
        public Node GetNode(char c, int create)
        {
            if (_size <= ARRAY_LENGTH_LIMIT)
            {
                var array = ChildrenArray;
                var node = BinarySearch(c, array, 0, _size);
                if (node == null && create == 1)
                {
                    if (_size < ARRAY_LENGTH_LIMIT)          // 数组容量未满
                    {
                        InsertInOrder(c);
                    }
                    else                                    // 数组容易已满，迁徙到映射表中
                    {
                        var map = ChildrenDict;
                        Migrate(array, map);
                        map.Add(c, new Node(c, _threadSafe));
                        _size++;

                        _childrenArray = null;      // 释放子节点数组
                    }
                }
                return node;
            }

            var dict = ChildrenDict;
            Node subNode;
            if (dict.TryGetValue(c, out subNode))
                return subNode;
            else if (create == 1)
            {
                subNode = new Node(c, _threadSafe);
                dict.Add(c, subNode);
                _size++;
            }
            return subNode;
        }

        /// <summary>
        /// 对数组进行二分查找
        /// </summary>
        /// <returns></returns>
        private Node BinarySearch(char c, Node[] array, int fromInclusive, int toExclusive)
        {
            if (fromInclusive >= toExclusive) return null;

            var m = (fromInclusive + toExclusive) / 2;
            var node = array[m];
            if (node == null) return null;

            if (node._char == c) return node;

            if (node._char < c) return BinarySearch(c, array, m + 1, toExclusive);

            return BinarySearch(c, array, fromInclusive, m);
        }

        /// <summary>
        /// 顺序插入一个新节点到子节点数组中
        /// </summary>
        private void InsertInOrder(char c)
        {
            int i;
            for (i = 0; i < _size; i++)
            {
                if (_childrenArray[i]._char > c)
                {
                    // 第一次发现更大的值，则新节点应该插入到此位置，然后原来此位置节点以及之后节点依次向后移位
                    break;
                }
            }
            _size++;
            for (int j = _size - 1; j > i; j--)
            {
                _childrenArray[j] = _childrenArray[j - 1];
            }
            _childrenArray[i] = new Node(c, _threadSafe);
        }

        /// <summary>
        /// 将子节点数组迁徙到子节点字典中
        /// </summary>
        /// <param name="array"></param>
        /// <param name="dict"></param>
        public void Migrate(Node[] array, Dictionary<char, Node> dict)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] != null)
                {
                    dict.Add(array[i]._char, array[i]);
                }
            }
        }
    }
}
