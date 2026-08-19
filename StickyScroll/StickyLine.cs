namespace StickyScroll
{
    /// <summary>
    /// 一条粘滞行：编辑器里一个作用域块（namespace/class/method...）的起始行。
    /// </summary>
    internal sealed class StickyLine
    {
        /// <summary>起始行号（0 基）。</summary>
        public readonly int LineNumber;

        /// <summary>该行去前导空白后的文本（用于展示）。</summary>
        public readonly string Text;

        /// <summary>该行原始前导空白宽度（用于缩进展示）。</summary>
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
