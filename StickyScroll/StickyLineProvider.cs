using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;

namespace StickyScroll
{
    /// <summary>
    /// 粘滞行检测器：给定快照与视口顶行号，计算应"粘滞"的作用域链。
    /// 主方案：编辑器原生 outlining 区域（语言服务提供的语法块，C# 由 Roslyn 提供，准确）；
    /// 回退方案：启发式花括号扫描器（用于无 outlining 的语言）。
    /// </summary>
    internal sealed class StickyLineProvider
    {
        private readonly IOutliningManagerService _outliningManagerService;

        // 扫描器缓存：按 snapshot 版本缓存候选行，避免每次滚动全量重扫
        private ITextSnapshot _cachedSnapshot;
        private List<ScannedLine> _cachedLines;

        public StickyLineProvider(IOutliningManagerService outliningManagerService)
        {
            _outliningManagerService = outliningManagerService;
        }

        /// <summary>
        /// 计算粘滞链：视口顶行所在的最深作用域，沿祖先链向上取 ≤ maxLines 条。
        /// </summary>
        public IList<StickyLine> GetStickyLines(ITextView view, int firstVisibleLine, int maxLines)
        {
            var lines = new List<StickyLine>(maxLines);

            var outlining = _outliningManagerService.GetOutliningManager(view);
            if (outlining != null && outlining.Enabled)
            {
                CollectFromOutlining(outlining, view.TextSnapshot, firstVisibleLine, maxLines, lines);
                if (lines.Count > 0)
                    return lines;
            }

            CollectFromScanner(view.TextSnapshot, firstVisibleLine, maxLines, lines);
            return lines;
        }

        // ---------------- Outlining 方案 ----------------

        private static void CollectFromOutlining(IOutliningManager outlining, ITextSnapshot snapshot,
            int firstVisibleLine, int maxLines, List<StickyLine> result)
        {
            // 所有区域（含折叠与展开）
            var regions = outlining.GetAllRegions(new SnapshotSpan(snapshot, 0, snapshot.Length)).ToList();
            if (regions.Count == 0)
                return;

            // 1) 找包含 firstVisibleLine 的最深区域（起始行 ≤ firstVisibleLine 且结束行 ≥ firstVisibleLine，
            //    起始行最靠后 = 最深）
            ICollapsible deepest = null;
            int deepestStart = int.MinValue;
            foreach (var r in regions)
            {
                int start = r.Extent.GetStartPoint(snapshot).GetContainingLine().LineNumber;
                int end = r.Extent.GetEndPoint(snapshot).GetContainingLine().LineNumber;
                if (start <= firstVisibleLine && end >= firstVisibleLine)
                {
                    if (start > deepestStart)
                    {
                        deepestStart = start;
                        deepest = r;
                    }
                }
            }

            if (deepest == null)
                return;

            // 2) 从最深区域开始，沿祖先链向上收集（父区域起点更早、终点更晚，起点最靠后者为最近父）
            var chain = new List<ICollapsible>();
            chain.Add(deepest);
            int cursorStart = deepestStart;
            while (chain.Count < maxLines)
            {
                ICollapsible parent = null;
                int parentStart = int.MinValue;
                foreach (var r in regions)
                {
                    if (chain.Contains(r))
                        continue;
                    int start = r.Extent.GetStartPoint(snapshot).GetContainingLine().LineNumber;
                    int end = r.Extent.GetEndPoint(snapshot).GetContainingLine().LineNumber;
                    if (start < cursorStart && end >= cursorStart && start > parentStart)
                    {
                        parentStart = start;
                        parent = r;
                    }
                }
                if (parent == null)
                    break;
                chain.Add(parent);
                cursorStart = parentStart;
            }

            // 反转：最外层在前
            chain.Reverse();

            // 剔除"起始行 ≥ 视口顶行"的层（该行本身已在视口内可见，无需粘滞）
            while (chain.Count > 0)
            {
                var last = chain[chain.Count - 1];
                int start = last.Extent.GetStartPoint(snapshot).GetContainingLine().LineNumber;
                if (start >= firstVisibleLine)
                    chain.RemoveAt(chain.Count - 1);
                else
                    break;
            }

            int depth = 0;
            foreach (var r in chain)
            {
                var startLine = r.Extent.GetStartPoint(snapshot).GetContainingLine();
                int lineNumber = startLine.LineNumber;
                string text = startLine.GetText();
                int indent = 0;
                while (indent < text.Length && (text[indent] == ' ' || text[indent] == '\t'))
                    indent++;
                if (text.Substring(indent).Trim().Length == 0)
                    continue; // 空行区域起点（如纯缩进行）不粘滞
                result.Add(new StickyLine(lineNumber, text.Substring(indent), indent, depth++));
            }
        }

        // ---------------- 启发式扫描器回退 ----------------

        private sealed class ScannedLine
        {
            public readonly int LineNumber;
            public readonly string Text;      // 去前导空白
            public readonly int Indent;
            public readonly int Depth;        // 该行起始时的括号深度
            public ScannedLine(int lineNumber, string text, int indent, int depth)
            {
                LineNumber = lineNumber;
                Text = text;
                Indent = indent;
                Depth = depth;
            }
        }

        private void CollectFromScanner(ITextSnapshot snapshot, int firstVisibleLine, int maxLines, List<StickyLine> result)
        {
            if (snapshot != _cachedSnapshot)
            {
                _cachedSnapshot = snapshot;
                _cachedLines = Scan(snapshot);
            }

            var lines = _cachedLines;
            if (lines.Count == 0)
                return;

            // 找起始行 ≤ firstVisibleLine 的最后一个候选（最深的 open block）
            int idx = -1;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (lines[i].LineNumber <= firstVisibleLine)
                {
                    idx = i;
                    break;
                }
            }
            if (idx < 0)
                return;

            // 沿祖先链收集：同一深度往前找最近的（缩进更浅的）open block
            var chain = new List<ScannedLine>();
            chain.Add(lines[idx]);
            int curDepth = lines[idx].Depth;
            while (chain.Count < maxLines && chain[chain.Count - 1].Depth > 0)
            {
                int targetDepth = chain[chain.Count - 1].Depth - 1;
                ScannedLine parent = null;
                for (int i = idx - 1; i >= 0; i--)
                {
                    var l = lines[i];
                    if (l.Depth == targetDepth && l.LineNumber < chain[chain.Count - 1].LineNumber)
                    {
                        parent = l;
                        break;
                    }
                }
                if (parent == null)
                    break;
                chain.Add(parent);
            }

            chain.Reverse();
            int d = 0;
            foreach (var l in chain)
                result.Add(new StickyLine(l.LineNumber, l.Text, l.Indent, d++));
        }

        /// <summary>
        /// 逐行扫描花括号语言：记录每个"块开启行"（行内含 `{` 且其后无实质代码），
        /// 状态机忽略注释、字符串（含 @""、$""）、字符字面量、预处理行。
        /// </summary>
        private static List<ScannedLine> Scan(ITextSnapshot snapshot)
        {
            var result = new List<ScannedLine>();
            int depth = 0;

            bool inBlockComment = false;

            for (int lineNumber = 0; lineNumber < snapshot.LineCount; lineNumber++)
            {
                string text = snapshot.GetLineFromLineNumber(lineNumber).GetText();
                if (text.Length == 0)
                    continue;

                int indent = 0;
                while (indent < text.Length && (text[indent] == ' ' || text[indent] == '\t'))
                    indent++;

                string trimmed = text.Substring(indent);
                if (trimmed.Length == 0)
                    continue;
                if (trimmed[0] == '#')
                    continue; // 预处理指令
                if (inBlockComment && !trimmed.Contains("*/"))
                    continue;

                int openBrace = -1;   // 最后一个 `{` 的位置（在行内实质代码中）

                int i = 0;
                bool inLineComment = false;
                bool inString = false;
                bool inChar = false;
                bool verbatim = false;   // @"..." 跨行

                while (i < text.Length)
                {
                    char c = text[i];
                    char next = (i + 1 < text.Length) ? text[i + 1] : '\0';

                    if (inLineComment)
                    {
                        i++;
                        continue;
                    }
                    if (inString || inChar)
                    {
                        if (verbatim && inString)
                        {
                            if (c == '"' && next == '"') { i += 2; continue; }
                            if (c == '"') { inString = false; }
                        }
                        else
                        {
                            if (c == '\\') { i += 2; continue; }
                            if (inString && c == '"') { inString = false; }
                            if (inChar && c == '\'') { inChar = false; }
                        }
                        i++;
                        continue;
                    }
                    if (inBlockComment)
                    {
                        if (c == '*' && next == '/') { inBlockComment = false; i += 2; }
                        else i++;
                        continue;
                    }

                    // 普通代码
                    if (c == '/' && next == '/') { inLineComment = true; i += 2; continue; }
                    if (c == '/' && next == '*') { inBlockComment = true; i += 2; continue; }
                    if (c == '"')
                    {
                        inString = true;
                        verbatim = (i > 0 && text[i - 1] == '@');
                        i++;
                        continue;
                    }
                    if (c == '\'') { inChar = true; i++; continue; }
                    if (c == '{') { openBrace = i; i++; continue; }
                    if (c == '}') { depth--; i++; continue; }
                    i++;
                }

                // 行尾是 `{` 的行视为块开启行（忽略只有 `{` 本身的行，如 "} else {" 也算）
                if (openBrace >= 0 && !inLineComment)
                {
                    // 检查 `{` 之后是否只有空白/注释
                    string after = text.Substring(openBrace + 1);
                    string afterTrimmed = after.TrimStart();
                    bool onlyWhitespace = afterTrimmed.Length == 0
                        || afterTrimmed.StartsWith("//")
                        || afterTrimmed.StartsWith("/*");

                    if (onlyWhitespace)
                    {
                        result.Add(new ScannedLine(lineNumber, trimmed, indent, depth));
                    }
                    depth++;
                }
            }

            return result;
        }
    }
}
