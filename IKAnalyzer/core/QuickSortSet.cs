using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKAnalyzer.core
{
    /// <summary>
    /// 快速排序集合
    /// 非线程安全
    /// </summary>
    /// <typeparam name="V"></typeparam>
    public class QuickSortSet<V> where V : class, IComparable<V>
    {
        private Cell _head;
        /// <summary>
        /// 链表头指针
        /// </summary>
        public Cell Head { get { return _head; } }

        public Cell _tail;
        /// <summary>
        /// 链表尾指针
        /// </summary>
        public Cell Tail { get { return _tail; } }
        private int _size;
        /// <summary>
        /// 链表大小
        /// </summary>
        public int Size { get { return _size; } }

        /// <summary>
        /// 插入新结点，并使所有结点保持从小到大的顺序
        /// </summary>
        /// <param name="v">给定新结点的关联值</param>
        /// <returns></returns>
        public bool Insert(V v)
        {
            var newCell = new Cell(v);
            if(_size == 0)
            {
                _head = _tail = newCell;
                _size++;
            }
            else
            {
                // 从链表尾部开始寻找合适的位置插入新结点
                var cur = _tail;
                while (cur != null)       // 从后往前，寻找合适的插入点
                {
                    var res = cur.CompareTo(newCell);
                    if (res > 0)            // 尚未找到第一个比新结点小的结点
                        cur = cur.Prev;
                    else if (res == 0)      // 已有相同结点，不执行插入，返回false
                        return false;
                    else                    // 找到第一个比新结点小的结点
                        break;
                }
                    
                if(cur != null)     // 新结点应该插入在该当前结点之后
                {
                    newCell.Next = cur.Next;
                    newCell.Prev = cur;

                    if (cur.Next != null)           // 如果当前结点不是尾结点
                        cur.Next.Prev = newCell;
                    else                            // 如果当前结点是尾结点，则更新尾结点
                        _tail = newCell;

                    cur.Next = newCell;
                }
                else                // 新结点应该插入在头结点之前
                {
                    _head.Prev = newCell;
                    newCell.Next = _head;
                    _head = newCell;        // 更新头结点
                }
                _size++;
            }
            return true;
        }

        /// <summary>
        /// 返回头结点的关联值
        /// </summary>
        /// <returns></returns>
        public V PeekFirst() => _head?.V;

        /// <summary>
        /// 删除头结点，并返回结点关联的值
        /// </summary>
        /// <returns></returns>
        public V PollFirst()
        {
            if (_size == 0) return null;

            V v = _head.V;
            _size--;
            if (_size == 0)
            {
                _head = null;
                _tail = null;
                
            }
            else
            {
                _head = _head.Next;
                _head.Prev = null;
            }
            return v;
        }
        /// <summary>
        /// 返回尾结点的值
        /// </summary>
        /// <returns></returns>
        public V PeekLast() => _tail?.V;
        /// <summary>
        /// 删除尾结点，并返回结点关联的值
        /// </summary>
        /// <returns></returns>
        public V PollLast()
        {
            if (_size == 0) return null;

            var v = _tail.V;
            _size--;
            if(_size == 0)
            {
                _tail = null;
                _head = null;
            }
            else
            {
                _tail = _tail.Prev;
                _tail.Next = null;
            }
            return v;
        }

        public class Cell : IComparable<Cell>
        {
            public Cell Prev { get; set; }
            public Cell Next { get; set; }
            public V V { get; set; }

            public Cell(V v)
            {
                V = v ?? throw new ArgumentNullException("param v is invalid");
            }

            public int CompareTo(Cell other) => this.V.CompareTo(other.V);
        }
    }
}
