using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using IKAnalyzer.cfg;

namespace IKAnalyzer.core
{
    public class CHSArea : Trie<string>
    {
        private const string AREA_TAIL = "省市县区州";
        public CHSArea()
        {
            LoadFromFile(DefaultConfig.Instance.CHSAreaPath);
        }

        public override void LoadFromFile(string path)
        {
            var lines = File.ReadAllLines(path);
            for(int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var segs = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                
                if(segs.Length == 1)    // 如果只有一个segment，则只有地区名，没有地区代码
                {
                    // 然而，这种情况需要确保地区名只表示地区，而不会表示其他名词，否则会弄错词的类型
                    Root.AddNode(segs[0], 0, null, true);
                }
                else
                {
                    var v = segs[0];
                    for(int j = 1; j < segs.Length; j++)
                    {
                        Root.AddNode(segs[j], 0, v, true);
                    }
                }
            }
        }

        public string Match(string input, int start, int length) => Root.Match(input, start, length);
        public string Match(string input) => Root.Match(input);

        /// <summary>
        /// 前缀匹配
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public HashSet<string> PrefixMatch(string input)
        {
            var set = new HashSet<string>();
            PrefixMatch(input, 0, Root, set);
            return set;
        }

        private void PrefixMatch(string input, int index, Node node, HashSet<string> set)
        {
            if (node.Children == null) return;      // 没有子节点，则直接返回
            var c = input[index];

            if (index == input.Length - 1)   // 匹配完了
            {
                if(node.Children.TryGetValue(c, out var subNode))   // 匹配成功
                {
                    if (subNode.V != null)
                        set.Add(subNode.V);
                    else        // 尝试加上地区名后再次匹配
                    {
                        foreach (var t in AREA_TAIL)
                        {
                            if (subNode.Children.TryGetValue(t, out var child))
                            {
                                if (child.Enabled && child.V != null)
                                    set.Add(child.V);
                            }
                        }
                    }
                }
            }
            else        // 尚未匹配完
            {
                if(node.Children.TryGetValue(c, out var subNode))
                {
                    if (subNode.V != null)
                        set.Add(subNode.V);
                    PrefixMatch(input, index + 1, subNode, set);    // 继续匹配
                }
                // else -> 否则，匹配失败，直接返回
            }
        }
    }
}
