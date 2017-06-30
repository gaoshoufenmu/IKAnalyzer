using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Threading.Tasks;

namespace IKAnalyzer.cfg
{
    /// <summary>
    /// 默认配置
    /// </summary>
    class DefaultConfig : IConfig
    {
        //! ------------------------  默认字典路径 ------------------------------------
        private readonly string ROOT_DIR = AppDomain.CurrentDomain.BaseDirectory;
        private const string MAIN_DICT_PATH = "/resource/main.dict";
        private const string QUANT_DICT_PATH = "/resource/quantifier.dict";
        private const string CONFIG_FILE_PATH = "/resource/IKAnalyzer.cfg.xml";
        private const string CHS_AREA_PATH = "/resource/chs_area.txt";


        private XmlDocument _xmlDoc;

        private static DefaultConfig _instance = new DefaultConfig();
        public static DefaultConfig Instance { get { return _instance; } }

        private DefaultConfig()
        {
            _xmlDoc = new XmlDocument();
            _xmlDoc.Load(ROOT_DIR + CONFIG_FILE_PATH);
        }
        /// <summary>
        /// 是否启用智能分词
        /// </summary>
        public bool UseSmart { get; set; }

        private List<string> _extDictPaths;
        /// <summary>
        /// 扩展词典路径
        /// </summary>
        public List<string> ExtDictPaths
        {
            get
            {
                if(_extDictPaths == null)
                {
                    var extNode = _xmlDoc.SelectSingleNode("//dictionary[@key='ext_dict']");
                    var paths = extNode.InnerText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    _extDictPaths = new List<string>(paths.Length);
                    for (int i = 0; i < paths.Length; i++)
                        _extDictPaths.Add($"{ROOT_DIR}/resource/{paths[i]}");
                }
                return _extDictPaths;
            }
        }
        private List<string> _extStopwordDictPaths;
        /// <summary>
        /// 扩展停用词词典路径
        /// </summary>
        public List<string> ExtStopwordDictPaths
        {
            get
            {
                if(_extStopwordDictPaths == null)
                {
                    var extNode = _xmlDoc.SelectSingleNode("//dictionary[@key='ext_stopword']");
                    var paths = extNode.InnerText.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    _extStopwordDictPaths = new List<string>(paths.Length);
                    for (int i = 0; i < paths.Length; i++)
                        _extStopwordDictPaths.Add($"{ROOT_DIR}/resource/{paths[i]}");
                }
                return _extStopwordDictPaths;
            }
        }

        public string MainDictPath { get => ROOT_DIR + MAIN_DICT_PATH; }

        public string QuantDictPath { get => ROOT_DIR + QUANT_DICT_PATH; }

        public string CHSAreaPath { get => ROOT_DIR + CHS_AREA_PATH; }
    }
}
