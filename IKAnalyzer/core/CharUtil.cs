using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;

namespace IKAnalyzer.core
{
    public class CharUtil
    {
        public const int CHAR_USELESS = 0;
        public const int CHAR_ARABIC = 0x00000001;
        public const int CHAR_ENGLISH = 0x00000002;
        public const int CHAR_CHINESE = 0x00000004;
        public const int CHAR_OTHER_CJK = 0x00000008;

        public static int GetCharType(char c)
        {
            if (c >= '0' && c <= '9') return CHAR_ARABIC;
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) return CHAR_ENGLISH;

            // 由于C#中没有java的UnicodeBlock，故这里简单的用Unicode的一个大致范围来判断
            if (c >= '\u4e00' && c <= '\u9fa5') return CHAR_CHINESE;
            if ((c >= '\u0800' && c <= '\u4e00') || (c >= '\uac00' && c <= '\ud7ff')) return CHAR_OTHER_CJK;

            return CHAR_USELESS;
        }

        /// <summary>
        /// 字符规范化
        /// 全角转半角，大写转小写
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static char Normalize(char c)
        {
            if (c == 12288)
                return (char)32;
            if (c > 65280 && c < 65375)
                return (char)(c - 65248);
            if (c >= 'A' && c <= 'Z')
                return (char)(c + 32);

            if (c == '（')
                c = '(';
            else if (c == '）')
                c = ')';
            else if (c == '【')
                c = '[';
            else if (c == '】')
                c = ']';
            return c;
        }
    }
}
