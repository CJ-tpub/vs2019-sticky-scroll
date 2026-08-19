namespace StickyScroll
{
    /// <summary>
    /// 一条粘滞行：编辑器里一个作用域块（namespace/class/method...）的起始行。
    /// </summary>
    internal sealed class StickyLine
    {
        /// <summary>起始行号（0 基）。</summary>
        public readonly int LineNumber;

        /// <summary>该行完整原始文本（含前导缩进，用于与原代码一致的渲染）。</summary>
        public readonly string Text;

        /// <summary>该行前导空白字符数（保留，供参考）。</summary>
        public readonly int IndentLength;

        /// <summary>作用域深度（0 = 最外层）。</summary>
        public readonly int Depth;

        public StickyLine(int lineNumber, string text, int indentLength, int depth)
        {
            LineNumber = lineNumber;
            Text = text;
            IndentLength = indentLength;
            Depth = depth;
        }
    }
}
