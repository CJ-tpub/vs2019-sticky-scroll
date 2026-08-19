using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace StickyScroll
{
    /// <summary>
    /// 粘滞滚动条（top margin）：固定在编辑器视口顶部，显示当前作用域链。
    /// 渲染目标：与原代码行视觉一致——保留原始缩进、编辑器背景色、语法高亮，
    /// 看上去就像原有部分行代码在下滑过程中滞留在了顶部。
    /// </summary>
    internal sealed class StickyScrollMargin : IWpfTextViewMargin
    {
        public const string MarginName = "StickyScrollMargin";

        // 默认显示的最大粘滞行数（后续由选项页接管）
        private const int DefaultMaxLines = 3;

        private readonly IWpfTextView _view;
        private readonly IWpfTextViewHost _textViewHost;
        private readonly StickyLineProvider _stickyLineProvider;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly IClassifierAggregatorService _classifierAggregatorService;
        private readonly IClassificationFormatMapService _classificationFormatMapService;
        private readonly StackPanel _root;
        private bool _isDisposed;

        // 分类器（按 buffer 缓存）
        private IClassifier _classifier;

        // 最近一次渲染的链与文本左缘（用于避免无谓重绘；TextLeft 变化也触发重绘）
        private IList<StickyLine> _lastLines = new StickyLine[0];
        private double _lastTextLeft = double.NaN;

        public StickyScrollMargin(
            IWpfTextViewHost textViewHost,
            StickyLineProvider stickyLineProvider,
            IEditorFormatMap editorFormatMap,
            IClassifierAggregatorService classifierAggregatorService,
            IClassificationFormatMapService classificationFormatMapService)
        {
            _view = textViewHost.TextView;
            _textViewHost = textViewHost;
            _stickyLineProvider = stickyLineProvider;
            _editorFormatMap = editorFormatMap;
            _classifierAggregatorService = classifierAggregatorService;
            _classificationFormatMapService = classificationFormatMapService;

            _root = new StackPanel
            {
                Orientation = Orientation.Vertical,
                ClipToBounds = false,   // 行号列需要绘制到 margin 左侧（行号 margin 区域）
                Focusable = false
            };

            // 事件
            _view.LayoutChanged += OnLayoutChanged;
            _view.TextBuffer.Changed += OnTextBufferChanged;
            _view.Closed += OnViewClosed;
            _editorFormatMap.FormatMappingChanged += OnFormatMappingChanged;
        }

        // ---------------- 事件处理 ----------------

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            // 滚动、缩放、字体变化、文本变化都会触发布局变化，统一刷新
            UpdateStickyLines();
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            UpdateStickyLines();
        }

        private void OnFormatMappingChanged(object sender, FormatItemsEventArgs e)
        {
            UpdateStickyLines();
        }

        private void OnViewClosed(object sender, EventArgs e)
        {
            Dispose();
        }

        // ---------------- 核心：计算 + 渲染 ----------------

        private void UpdateStickyLines()
        {
            if (_isDisposed || _view.IsClosed)
                return;

            // 首可见行
            ITextViewLine firstLine;
            try
            {
                if (_view.TextViewLines == null || _view.TextViewLines.Count == 0)
                    return;
                firstLine = _view.TextViewLines.FirstVisibleLine;
            }
            catch (InvalidOperationException)
            {
                return; // 布局未就绪
            }

            int firstVisibleLineNumber = firstLine.Start.GetContainingLine().LineNumber;

            var lines = _stickyLineProvider.GetStickyLines(_view, firstVisibleLineNumber, DefaultMaxLines);

            // 文本区左缘（与编辑器文本列对齐；水平滚动时变化）
            double textLeft = 0;
            try
            {
                textLeft = firstLine.TextLeft;
            }
            catch
            {
                // 布局未就绪时用 0
            }

            // 避免无谓重绘：链相同且文本左缘未变则跳过
            if (SameChain(_lastLines, lines) && Math.Abs(_lastTextLeft - textLeft) < 0.5)
                return;
            _lastLines = lines;
            _lastTextLeft = textLeft;

            Render(lines);
        }

        private static bool SameChain(IList<StickyLine> a, IList<StickyLine> b)
        {
            if (a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].LineNumber != b[i].LineNumber)
                    return false;
            }
            return true;
        }

        private IClassifier GetClassifier()
        {
            if (_classifier == null)
            {
                _classifier = _classifierAggregatorService.GetClassifier(_view.TextBuffer);
            }
            return _classifier;
        }

        private void Render(IList<StickyLine> lines)
        {
            _root.Children.Clear();

            if (lines.Count == 0)
                return;

            // 背景：浅蓝色（与编辑器白色背景区分；深色主题自动派生蓝灰色调）
            var background = GetBrush(EditorFormatDefinition.BackgroundBrushId, null) as SolidColorBrush;
            _root.Background = MakeLightBlue(background);

            // 前景 fallback
            var foreground = GetBrush(EditorFormatDefinition.ForegroundBrushId, Brushes.Gray);

            // 字体：与编辑器完全一致（DefaultTextProperties 为未缩放基准，乘 ZoomLevel 得到实际渲染大小）
            var defaultProps = _view.FormattedLineSource != null
                ? _view.FormattedLineSource.DefaultTextProperties
                : null;
            Typeface typeface = defaultProps != null ? defaultProps.Typeface : new Typeface("Consolas");
            double zoom = _view.ZoomLevel > 0 ? _view.ZoomLevel : 100.0;
            double fontSize = defaultProps != null
                ? defaultProps.FontRenderingEmSize * zoom / 100.0
                : 14.0;

            // 行高：与编辑器文本行一致，略微拉高（更舒展）
            double lineHeight = _view.LineHeight > 0 ? _view.LineHeight : fontSize * 1.4;
            double rowHeight = lineHeight * 1.12;

            // Tab 宽度（编辑器设置，用于前导 Tab 展开对齐）
            int tabSize = 4;
            try
            {
                tabSize = _view.Options.GetOptionValue<int>(DefaultOptions.TabSizeOptionId);
            }
            catch
            {
                // 默认 4
            }

            // 行号列宽：与编辑器行号 margin 完全一致（用户关闭行号时为 0，不显示行号）
            double lineNumberWidth = GetLineNumberMarginWidth(fontSize);

            // 行号颜色：与编辑器行号近似的灰色
            var lineNumberBrush = new SolidColorBrush(Color.FromRgb(0x6D, 0x6D, 0x6D));
            lineNumberBrush.Freeze();

            var classifier = GetClassifier();
            var formatMap = _classificationFormatMapService.GetClassificationFormatMap(_view);

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                // 每行 = [行号列][文本列]，行号列宽与编辑器行号 margin 一致；固定行高（拉高更舒展）
                var grid = new Grid
                {
                    Height = rowHeight
                };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(lineNumberWidth) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // 行号：绘制在行号 margin 区域内（margin 左缘左侧），数字右缘 = 行号 margin 右缘 = 编辑器行号数字右缘
                var ln = new TextBlock
                {
                    Text = (line.LineNumber + 1).ToString(),
                    FontFamily = typeface.FontFamily,
                    FontSize = fontSize,
                    FontStyle = typeface.Style,
                    FontWeight = typeface.Weight,
                    Foreground = lineNumberBrush,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = lineNumberWidth,
                    Margin = new Thickness(-lineNumberWidth, 0, 0, 0),
                    TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(ln, 0);

                // 代码文本（文本列起点 = 编辑器文本左缘）
                var tb = new TextBlock
                {
                    FontFamily = typeface.FontFamily,
                    FontSize = fontSize,
                    FontStyle = typeface.Style,
                    FontWeight = typeface.Weight,
                    Margin = new Thickness(Math.Max(0, _lastTextLeft - lineNumberWidth), 0, 0, 0),
                    Padding = new Thickness(0),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Cursor = Cursors.Hand,
                    ToolTip = "Sticky scroll: line " + (line.LineNumber + 1) + ": " + line.Text.Trim()
                };
                Grid.SetColumn(tb, 1);

                // 前导 Tab 按编辑器 TabSize 展开为空格（TextBlock 的 Tab 宽度与编辑器不一致，必须展开）
                string displayText = ExpandLeadingTabs(line.Text, tabSize);

                // 语法高亮：分类器对原行文本着色（与原代码渲染一致）
                AppendClassifiedRuns(tb, line, displayText, classifier, formatMap, foreground);

                int targetLine = line.LineNumber;
                grid.MouseLeftButtonUp += (s, e2) => ScrollToLine(targetLine);
                grid.MouseEnter += (s, e2) =>
                {
                    var hover = new SolidColorBrush(Color.FromArgb(48, 128, 128, 128));
                    hover.Freeze();
                    grid.Background = hover;
                };
                grid.MouseLeave += (s, e2) => grid.Background = null;

                grid.Children.Add(ln);
                grid.Children.Add(tb);
                _root.Children.Add(grid);
            }

            // 粘滞栏与代码区之间：一条很细的黑线（不太明显）
            var bottomLine = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0));
            bottomLine.Freeze();
            _root.Children.Add(new Border { Height = 1, Background = bottomLine });
        }

        /// <summary>
        /// 编辑器行号 margin 的宽度；拿不到时按行号位数估算（行号关闭时为 0）。
        /// </summary>
        private double GetLineNumberMarginWidth(double fontSize)
        {
            try
            {
                var m = _textViewHost.GetTextViewMargin(PredefinedMarginNames.LineNumber);
                if (m != null)
                {
                    double w = m.VisualElement.ActualWidth;
                    if (w <= 0)
                        w = m.VisualElement.DesiredSize.Width;
                    if (w > 0)
                        return w;
                }
            }
            catch
            {
                // fallthrough
            }

            // fallback：按位数估算（2 位行号 + padding）
            double charWidth = fontSize * 0.6;
            return 4 * charWidth + 12;
        }

        /// <summary>
        /// 浅蓝色背景：编辑器背景色与中蓝色按 8:2 混合（浅色主题=浅蓝，深色主题=蓝灰）。
        /// </summary>
        private static Brush MakeLightBlue(SolidColorBrush editorBackground)
        {
            Color bg = editorBackground != null ? editorBackground.Color : Colors.White;
            var brush = new SolidColorBrush(Color.FromRgb(
                (byte)(bg.R * 0.8 + 0x7A * 0.2),
                (byte)(bg.G * 0.8 + 0xA7 * 0.2),
                (byte)(bg.B * 0.8 + 0xD8 * 0.2)));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// 把行首的 Tab 展开为空格（按编辑器 TabSize 对齐制表位），保证与编辑器内缩进列一致。
        /// 返回展开后的完整文本。
        /// </summary>
        private static string ExpandLeadingTabs(string text, int tabSize)
        {
            if (text.Length == 0 || tabSize < 1)
                return text;
            int i = 0;
            while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
                i++;
            if (i == 0)
                return text;

            var sb = new StringBuilder(text.Length + 8);
            int col = 0;
            for (int j = 0; j < i; j++)
            {
                if (text[j] == '\t')
                {
                    int spaces = tabSize - (col % tabSize);
                    sb.Append(' ', spaces);
                    col += spaces;
                }
                else
                {
                    sb.Append(' ');
                    col++;
                }
            }
            sb.Append(text, i, text.Length - i);
            return sb.ToString();
        }

        /// <summary>
        /// 用分类器对行文本着色，追加 Runs 到 TextBlock；无分类结果时用纯前景色。
        /// displayText 为前导 Tab 展开后的显示文本；分类 spans 基于原始快照行（位置整体平移 expansion）。
        /// </summary>
        private void AppendClassifiedRuns(TextBlock tb, StickyLine line, string displayText, IClassifier classifier,
            IClassificationFormatMap formatMap, Brush fallbackForeground)
        {
            if (classifier == null || formatMap == null)
            {
                tb.Text = displayText;
                tb.Foreground = fallbackForeground;
                return;
            }

            try
            {
                var snapshot = _view.TextSnapshot;
                if (line.LineNumber >= snapshot.LineCount)
                {
                    tb.Text = displayText;
                    tb.Foreground = fallbackForeground;
                    return;
                }
                var snapshotLine = snapshot.GetLineFromLineNumber(line.LineNumber);
                var spans = classifier.GetClassificationSpans(snapshotLine.Extent);

                if (spans == null || spans.Count == 0)
                {
                    tb.Text = displayText;
                    tb.Foreground = fallbackForeground;
                    return;
                }

                // 前导 Tab 展开导致的长度差（分类 span 位置整体平移）
                int expansion = displayText.Length - line.Text.Length;

                int pos = 0;
                foreach (var span in spans.OrderBy(s => s.Span.Start.Position))
                {
                    int start = span.Span.Start.Position - snapshotLine.Start.Position;
                    int length = span.Span.Length;
                    if (length <= 0)
                        continue;

                    int dStart = start + expansion;
                    if (dStart > pos)
                        tb.Inlines.Add(new Run(displayText.Substring(pos, dStart - pos)) { Foreground = fallbackForeground });

                    var props = formatMap.GetTextProperties(span.ClassificationType);
                    var run = new Run(displayText.Substring(dStart, length))
                    {
                        Foreground = props.ForegroundBrush ?? fallbackForeground
                    };
                    if (props.Bold)
                        run.FontWeight = FontWeights.Bold;
                    tb.Inlines.Add(run);

                    pos = dStart + length;
                }

                if (pos < displayText.Length)
                    tb.Inlines.Add(new Run(displayText.Substring(pos)) { Foreground = fallbackForeground });
            }
            catch
            {
                // 分类失败时退化为纯文本
                tb.Text = displayText;
                tb.Foreground = fallbackForeground;
            }
        }

        private void ScrollToLine(int lineNumber)
        {
            if (_view.IsClosed)
                return;
            try
            {
                double lineHeight = _view.LineHeight > 0 ? _view.LineHeight : 14.0;
                double targetY = lineNumber * lineHeight;
                double delta = targetY - _view.ViewportTop;
                _view.ViewScroller.ScrollViewportVerticallyByPixels(delta);
            }
            catch (InvalidOperationException)
            {
                // 视图关闭等竞态
            }
        }

        private Brush GetBrush(string itemKey, Brush fallback)
        {
            try
            {
                var props = _editorFormatMap.GetProperties("Plain Text");
                if (props[itemKey] is Brush b)
                    return b;
            }
            catch
            {
                // fallthrough
            }
            return fallback;
        }

        // ---------------- IWpfTextViewMargin ----------------

        public FrameworkElement VisualElement => _root;

        public double MarginSize => _root.ActualHeight > 0 ? _root.ActualHeight : _root.DesiredSize.Height;

        public bool Enabled => true;

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return string.Equals(marginName, MarginName, StringComparison.Ordinal) ? this : null;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            _view.LayoutChanged -= OnLayoutChanged;
            _view.TextBuffer.Changed -= OnTextBufferChanged;
            _view.Closed -= OnViewClosed;
            _editorFormatMap.FormatMappingChanged -= OnFormatMappingChanged;
            _root.Children.Clear();
        }
    }
}
