using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace IKAnalyzer.core
{
    /// <summary>
    /// 对字节数组进行封装，使得读取时能记录读取位置
    /// </summary>
    public class ByteArray : IDisposable
    {
        private byte[] _bytes;
        private int _offset;
        public byte[] Bytes { get => _bytes; }
        public int Offset { get => _offset; }
        public ByteArray(byte[] bytes) => _bytes = bytes;

        public static ByteArray Create(string path) =>
            File.Exists(path) ? new ByteArray(File.ReadAllBytes(path)) : null;

        public int NextInt()
        {
            var res = BitConverter.ToInt32(_bytes, _offset);
            _offset += 4;
            return res;
        }

        public double NextDouble()
        {
            var res = BitConverter.ToDouble(_bytes, _offset);
            _offset += 8;
            return res;
        }

        public char NextChar()
        {
            var res = BitConverter.ToChar(_bytes, _offset);
            _offset += 2;
            return res;
        }

        public byte NextByte() => _bytes[_offset++];

        public bool HasMore() => _offset < _bytes.Length;

        public string NextString()
        {
            char[] buffer = new char[NextInt()];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = NextChar();
            }
            return new string(buffer);
        }

        public float NextFloat()
        {
            var res = BitConverter.ToSingle(_bytes, _offset);
            _offset += 4;
            return res;
        }

        public byte[] NextBytes(int len)
        {
            var bytes = new byte[len];
            Array.Copy(_bytes, _offset, bytes, 0, len);
            return bytes;
        }

        public ushort NextUShort()
        {
            var res = BitConverter.ToUInt16(_bytes, _offset);
            _offset += 4;
            return res;
        }

        /// <summary>
        /// Read the next UTF string
        /// </summary>
        /// <returns></returns>
        public string NextUTFStr()
        {
            var len = NextUShort();
            var bs = new byte[len];
            var cs = new char[len];

            for (int i = 0; i < len; i++)
                bs[i] = NextByte();

            int count = 0;
            int idx = 0;
            while (count < len)
            {
                int c = bs[count] & 0xff;
                if (c > 127) break;

                cs[count++] = (char)c;
            }
            idx = count;
            int c2, c3;
            while (count < len)
            {
                // enters this code snippet means that "c > 127"
                int c = bs[count] & 0xff;

                switch (c >> 4)
                {
                    case int cc when cc < 8:
                        count++;
                        cs[idx++] = (char)c;
                        break;
                    case 12:
                    case 13:
                        count++;
                        if (count >= len)
                        {
                            throw new Exception("malformed input: partial character at end");
                        }
                        c2 = bs[count++];
                        if ((c2 & 0xC0) != 0x80)
                        {
                            throw new Exception($"malformed input around byte {count}");
                        }
                        cs[idx++] = (char)(((c & 0x1F) << 6) | (c2 & 0x3F));
                        break;
                    case 14:
                        count += 3;
                        if (count > len)
                            throw new Exception("malformed input: partial character at end");
                        c2 = bs[count - 2];
                        c3 = bs[count - 1];
                        if (((c2 & 0xC0) != 0x80) || ((c3 & 0xC0) != 0x80))
                            throw new Exception($"malformed input around byte {count - 1}");
                        cs[idx++] = (char)(((c & 0x0F) << 12) | ((c2 & 0x3F) << 6) | ((c3 & 0x3F) << 0));
                        break;
                    default:
                        throw new Exception($"malformed input around byte {count}");
                }

            }
            return new string(cs, 0, idx);
        }

        public void Dispose() => _bytes = null;
    }
}
