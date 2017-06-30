using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKAnalyzer.cfg
{
    public interface IConfig
    {
        /// <summary>
        /// 是否启用智能分词
        /// </summary>
        bool UseSmart { get; set; }
        /// <summary>
        /// 主词典路径
        /// </summary>
        /// <returns></returns>
        string MainDictPath { get; }
        /// <summary>
        /// 量词词典路径
        /// </summary>
        /// <returns></returns>
        string QuantDictPath { get; }
        /// <summary>
        /// 扩展词典路径
        /// </summary>
        /// <returns></returns>
        List<string> ExtDictPaths { get; }
        /// <summary>
        /// 扩展停用词路径
        /// </summary>
        /// <returns></returns>
        List<string> ExtStopwordDictPaths { get; }
    }
}
