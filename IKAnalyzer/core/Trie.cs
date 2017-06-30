using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKAnalyzer.core
{
    public abstract class Trie<V>
    {
        public abstract void LoadFromFile(string path);
        public Node Root { get => _root; }

        private Node _root = new Node('\0');

        

        public class Node
        {
            private Dictionary<char, Node> _children;
            public Dictionary<char, Node> Children { get => _children; }
            /// <summary>
            /// 当前结点对应的字符
            /// </summary>
            private char _char;
            /// <summary>
            /// 关联值
            /// 只有那些从根结点到当前结点的路径表示一个完整有效的字符串时，才有关联值
            /// </summary>
            private V _v;
            /// <summary>
            /// 结点关联值
            /// </summary>
            public V V { get => _v; }

            private bool _enabled;
            public bool Enabled { get => _enabled; }

            public Node(char @char) => _char = @char;

            /// <summary>
            /// 添加子节点
            /// </summary>
            /// <param name="input"></param>
            /// <param name="index"></param>
            /// <param name="v"></param>
            /// <param name="enabled"></param>
            public void AddNode(string input, int index, V v, bool enabled)
            {
                var c = input[index];
                var node = GetNode(c, true);

                if (index == input.Length - 1)
                {
                    node._v = v;
                    node._enabled = enabled;
                }
                else
                    node.AddNode(input, index + 1, v, enabled);
            }

            /// <summary>
            /// 根据指定键值获取子节点
            /// </summary>
            /// <param name="c"></param>
            /// <param name="create">子节点不存在时是否创建</param>
            /// <returns></returns>
            private Node GetNode(char c, bool create)
            {
                if (create)
                {
                    if (_children == null)
                        _children = new Dictionary<char, Node>();
                    if (_children.TryGetValue(c, out var node))
                        return node;
                    else
                    {
                        var subNode = new Node(c);
                        _children.Add(c, subNode);
                        return subNode;
                    }
                }
                else if (_children != null && _children.TryGetValue(c, out var node))
                    return node;
                return null;
            }

            public V Match(string input, int start, int length)
            {
                var c = input[start];
                var node = GetNode(c, false);
                if(node != null)
                {
                    if (length > 1)
                        return node.Match(input, start + 1, length - 1);
                    else if (length == 1)
                        return node._v;
                }
                return default(V);
            }
            public V Match(string input) => Match(input, 0, input.Length);
        }

    }
}
