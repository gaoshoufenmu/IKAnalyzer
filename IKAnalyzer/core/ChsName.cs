using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IKAnalyzer.cfg;

namespace IKAnalyzer.core
{
    public class ChsName
    {
        /// <summary>
        /// 没有明显歧异的姓氏
        /// </summary>
        readonly static string[] FAMILY_NAMES = {
            //有明显歧异的姓氏
            "王","张","黄","周","徐",
            "胡","高","林","马","于",
            "程","傅","曾","叶","余",
            "夏","钟","田","任","方",
            "石","熊","白","毛","江",
            "史","候","龙","万","段",
            "雷","钱","汤","易","常",
            "武","赖","文", "查",

            //没有明显歧异的姓氏
            "赵", "肖", "孙", "李",
            "吴", "郑", "冯", "陈",
            "褚", "卫", "蒋", "沈",
            "韩", "杨", "朱", "秦",
            "尤", "许", "何", "吕",
            "施", "桓", "孔", "曹",
            "严", "华", "金", "魏",
            "陶", "姜", "戚", "谢",
            "邹", "喻", "柏", "窦",
            "苏", "潘", "葛", "奚",
            "范", "彭", "鲁", "韦",
            "昌", "俞", "袁", "酆",
            "鲍", "唐", "费", "廉",
            "岑", "薛", "贺", "倪",
            "滕", "殷", "罗", "毕",
            "郝", "邬", "卞", "康",
            "卜", "顾", "孟", "穆",
            "萧", "尹", "姚", "邵",
            "湛", "汪", "祁", "禹",
            "狄", "贝", "臧", "伏",
            "戴", "宋", "茅", "庞",
            "纪", "舒", "屈", "祝",
            "董", "梁", "杜", "阮",
            "闵", "贾", "娄", "颜",
            "郭", "邱", "骆", "蔡",
            "樊", "凌", "霍", "虞",
            "柯", "昝", "卢", "柯",
            "缪", "宗", "丁", "贲",
            "邓", "郁", "杭", "洪",
            "崔", "龚", "嵇", "邢",
            "滑", "裴", "陆", "荣",
            "荀", "惠", "甄", "芮",
            "羿", "储", "靳", "汲",
            "邴", "糜", "隗", "侯",
            "宓", "蓬", "郗", "仲",
            "栾", "钭", "历", "戎",
            "刘", "詹", "幸", "韶",
            "郜", "黎", "蓟", "溥",
            "蒲", "邰", "鄂", "咸",
            "卓", "蔺", "屠", "乔",
            "郁", "胥", "苍", "莘",
            "翟", "谭", "贡", "劳",
            "冉", "郦", "雍", "璩",
            "桑", "桂", "濮", "扈",
            "冀", "浦", "庄", "晏",
            "瞿", "阎", "慕", "茹",
            "习", "宦", "艾", "容",
            "慎", "戈", "廖", "庾",
            "衡", "耿", "弘", "匡",
            "阙", "殳", "沃", "蔚",
            "夔", "隆", "巩", "聂",
            "晁", "敖", "融", "訾",
            "辛", "阚", "毋", "乜",
            "鞠", "丰", "蒯", "荆",
            "竺", "盍", "单", "欧",

            //复姓必须在单姓后面
            "司马", "上官", "欧阳",
            "夏侯", "诸葛", "闻人",
            "东方", "赫连", "皇甫",
            "尉迟", "公羊", "澹台",
            "公冶", "宗政", "濮阳",
            "淳于", "单于", "太叔",
            "申屠", "公孙", "仲孙",
            "轩辕", "令狐", "徐离",
            "宇文", "长孙", "慕容",
            "司徒", "司空", "万俟"};

        /// <summary>
        /// key -> 姓氏第一个字
        /// value -> null: 单姓，list: 第一个char为'\0'表示单姓，其余char对应复姓的第二个字符
        /// </summary>
        Dictionary<char, List<char>> _FamilyNameDict = new Dictionary<char, List<char>>();
        HashSet<char> _SingleNameDict = new HashSet<char>();
        HashSet<char> _DoubleName1Dict = new HashSet<char>();
        HashSet<char> _DoubleName2Dict = new HashSet<char>();

        

        public ChsName()
        {
            foreach (string familyName in FAMILY_NAMES)
            {
                if (familyName.Length == 1)
                {
                    if (!_FamilyNameDict.ContainsKey(familyName[0]))
                    {
                        _FamilyNameDict.Add(familyName[0], null);
                    }
                }
                else
                {
                    List<char> sec = new List<char>();
                    if (_FamilyNameDict.ContainsKey(familyName[0]))
                    {
                        if (_FamilyNameDict[familyName[0]] == null)
                        {
                            sec.Add((char)0);
                            _FamilyNameDict[familyName[0]] = sec;
                        }

                        _FamilyNameDict[familyName[0]].Add(familyName[1]);
                    }
                    else
                    {
                        sec.Add(familyName[1]);
                        _FamilyNameDict[familyName[0]] = sec;
                    }
                }
            }
        }

        private HashSet<char> GetNameSet(string filePath)
        {
            var set = new HashSet<char>();

            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
            for(int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                char n = line[0];
                if (!set.Contains(n))
                    set.Add(n);
            }
            return set;
        }

        public void LoadChsName(string dictPath)
        {
            _SingleNameDict = GetNameSet(DefaultConfig.Instance.ChsSingleNameFileName);
            _DoubleName1Dict = GetNameSet(DefaultConfig.Instance.ChsDoubleName1FileName);
            _DoubleName2Dict = GetNameSet(DefaultConfig.Instance.ChsDoubleName2FileName);
        }

        /// <summary>
        /// 给定一个字符串，判断其是否是人名
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public NameMatch Match(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 2) return null;

            var nm = new NameMatch();

            var f1 = text[0];       // family name 1
            var f2 = text[1];       // family name 2
            var sf = false;         // 是否可以作为单姓
            var df = false;         // 是否可以作为复姓

            if(_FamilyNameDict.TryGetValue(f1, out var list))
            {
                // 存在姓的第一个字为c1
                if(list == null)
                {
                    // 单姓
                    sf = true;
                }
                else
                {
                    // 单姓或者复姓
                    foreach(var c in list)
                    {
                        if(c == f2)         // 复姓
                        {
                            df = true;
                            break;
                        }
                        else if (c == 0)    // 单姓
                        {
                            sf = true;
                        }
                    }
                }
            }
            if(sf)      // 如果可以作为单姓
            {
                if(text.Length == 2)    // 尝试匹配单名
                {
                    if(_SingleNameDict.Contains(f2))
                    {
                        nm.IsName = true;
                        nm.Candidates.Add($"{f1}/{f2}");
                    }
                }
                else if(text.Length == 3)
                {
                    if(_DoubleName1Dict.Contains(f2) && _DoubleName2Dict.Contains(text[2]))   // 双名匹配
                    {
                        nm.IsName = true;
                        nm.IsStrong = true;
                        nm.Candidates.Add($"{f1}/{text.Substring(1)}");
                    }
                }
            }
            if(df)
            {
                if(text.Length == 3)
                {
                    if (_SingleNameDict.Contains(f2))
                    {
                        nm.IsName = true;
                        nm.IsStrong = true;
                        nm.Candidates.Add($"{text.Substring(0, 2)}/{text[2]}");
                    }
                }
                else if (text.Length == 4)
                {
                    if (_DoubleName1Dict.Contains(f2) && _DoubleName2Dict.Contains(text[2]))   // 双名匹配
                    {
                        nm.IsName = true;
                        nm.IsStrong = true;
                        nm.Candidates.Add($"{text.Substring(0, 2)}/{text.Substring(2)}");
                    }
                }
            }
            return nm;
        }
    }

    public class NameMatch
    {
        /// <summary>
        /// 是否可以是姓名
        /// </summary>
        public bool IsName;
        /// <summary>
        /// 是否很强烈作为姓名
        /// </summary>
        public bool IsStrong;
        /// <summary>
        /// 所有可能的人名
        /// </summary>
        public List<string> Candidates = new List<string>();
    }
}
