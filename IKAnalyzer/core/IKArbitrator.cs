using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKAnalyzer.core
{
    /// <summary>
    /// IK 分词歧义裁决器
    /// </summary>
    public class IKArbitrator
    {
        /// <summary>
        /// 分词歧义处理
        /// </summary>
        /// <param name="context"></param>
        /// <param name="useSmart"></param>
        public void Process(AnalyzeContext context, bool useSmart)
        {
            var lexs = context.RawLexemes;      // 原始词元

            var lex = lexs.PollFirst();
            var overlapPath = new LexemePath();
            while(lex != null)
            {
                // lex没有添加进overlapPath，此时 overlapPath.Size > 0
                if (!overlapPath.ExpandOverlapLexeme(lex))   
                {
                    if (overlapPath.Size == 1 || !useSmart)     // 词元链中只有一个词元，或者不使用智能分词时，不进行歧义处理，直接添加到context中
                        context.AddLexemePath(overlapPath);
                    else                                        // 否则，进行歧义处理
                    {
                        // overlapPath.Size > 1
                        var head = overlapPath.Head;
                        var judgePath = Judge(head, overlapPath.PathSpan);
                        context.AddLexemePath(judgePath);
                    }

                    overlapPath = new LexemePath();
                    overlapPath.ExpandOverlapLexeme(lex);
                }
                lex = lexs.PollFirst();
            }

            // 退出循环后最后再处理 overlapPath
            if (overlapPath.Size == 1 || !useSmart)
                context.AddLexemePath(overlapPath);
            else
            {
                var head = overlapPath.Head;
                context.AddLexemePath(Judge(head, overlapPath.PathSpan));
            }
        }

        /// <summary>
        /// 歧义识别
        /// </summary>
        /// <param name="cell"></param>
        /// <param name="fullTextLen"></param>
        /// <returns></returns>
        public LexemePath Judge(QuickSortSet<Lexeme>.Cell cell, int fullTextLen)
        {
            // 无冲突的词元链候选集合
            var pathOptions = new SortedSet<LexemePath>();
            // 用于存储无冲突词元的词元链
            var option = new LexemePath();
            var stack = ForwardPath(cell, option);

            pathOptions.Add(option.Copy());

            while(stack.Count > 0)
            {
                var curCell = stack.Pop();
                // 回滚词元链
                BackPath(curCell.V, option);
                // 从当前歧义位置开始，前向获取无冲突的词元
                ForwardPath(curCell, option);
                pathOptions.Add(option.Copy());
            }
            return pathOptions.First();    // 排名越靠前的是越优的分词方案
        }

        /// <summary>
        /// 前向遍历，将有无冲突的词元添加进path，有冲突的词元添加入栈
        /// </summary>
        /// <param name="cell"></param>
        /// <param name="path"></param>
        /// <returns>返回有冲突的词元栈</returns>
        private Stack<QuickSortSet<Lexeme>.Cell> ForwardPath(QuickSortSet<Lexeme>.Cell cell, LexemePath path)
        {
            // 发生冲突的 Lexeme 栈
            var stack = new Stack<QuickSortSet<Lexeme>.Cell>();
            var cur = cell;
            while(cur != null && cur.V != null)
            {
                if(!path.ExpandNonOverlapLexeme(cur.V)) // cur.V与path有冲突，cur.V没有被添加进path
                {
                    // 词元交叉，添加失败
                    stack.Push(cur);
                }
                cur = cur.Next;
            }
            return stack;
        }

        /// <summary>
        /// 回滚词元链，直到词元链能接受指定的词元
        /// </summary>
        /// <param name="lex"></param>
        /// <param name="path"></param>
        private void BackPath(Lexeme lex, LexemePath path)
        {
            while (path.CheckOverlap(lex))
                path.RemoveTail();
        }
    }
}
