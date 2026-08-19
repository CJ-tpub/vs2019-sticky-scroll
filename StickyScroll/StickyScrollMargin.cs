using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly StickyLineProvider _stickyLineProvider;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly IClassifierAggregatorService _classifierAggregatorService;
        private readonly IClassificationFormatMapService _classificationFormatMapService;
        private readonly StackPanel _root;
        private bool _isDisposed;

        // 分类器（按 buffer 缓存）
        private IClassifier _classifier;

        // 最近一次渲染的链（用于避免无谓重绘）
        private IList<StickyLine> _lastLines = new StickyLine[0];

        public StickyScrollMargin(
            IWpfTextViewHost textViewHost,
            StickyLineProvider stickyLineProvider,
            IEditorFormatMap editorFormatMap,
            IClassifierAggregatorService classifierAggregatorService,
            IClassificationFormatMapService classificationFormatMapService)
        {
            _view = textViewHost.TextView;
            _stickyLineProvider = stickyLineProvider;
            _editorFormatMap = editorFormatMap;
            _classifierAggregatorService = classifierAggregatorService;
            _classificationFormatMapService = classificationFormatMapService;

            _root = new StackPanel
            {
                Orientation = Orientation.Vertical,
                ClipToBounds = true,
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

            // 避免无谓重绘：链相同则跳过
            if (SameChain(_lastLines, lines))
                return;
            _lastLines = lines;

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

            // 编辑器背景（不透明，视觉上与代码区一致）
            var background = GetBrush(EditorFormatDefinition.BackgroundBrushId, null);
            var bgBrush = background as SolidColorBrush;
            _root.Background = bgBrush ?? Brushes.White;

            // 前景 fallback
            var foreground = GetBrush(EditorFormatDefinition.ForegroundBrushId, Brushes.Gray);

            // 字体（与编辑器一致，含缩放）
            Typeface typeface = _view.FormattedLineSource != null
                ? _view.FormattedLineSource.DefaultTextProperties.Typeface
                : new Typeface("Consolas");
            double fontSize = _view.FormattedLineSource != null
                ? _view.FormattedLineSource.DefaultTextProperties.FontRenderingEmSize
                : 14.0;

            // 文本区左缘（与编辑器文本列对齐，实现缩进位置一致）
            double textLeft = 0;
            try
            {
                textLeft = _view.TextViewLines.FirstVisibleLine.TextLeft;
            }
            catch
            {
                // 布局未就绪时用 0
            }

            double lineHeight = _view.LineHeight > 0 ? _view.LineHeight : fontSize * 1.4;

            var classifier = GetClassifier();
            var formatMap = _classificationFormatMapService.GetClassificationFormatMap(_view);

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                var tb = new TextBlock
                {
                    FontFamily = typeface.FontFamily,
                    FontSize = fontSize,
                    FontStyle = typeface.Style,
                    FontWeight = typeface.Weight,
                    Margin = new Thickness(textLeft, 0, 0, 0),  // 与编辑器文本列对齐（缩进位置一致）
                    Padding = new Thickness(0),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Cursor = Cursors.Hand,
                    ToolTip = "Sticky scroll: " + line.Text.Trim()
                };

                // 语法高亮：分类器对原行文本着色（与原代码渲染一致）
                AppendClassifiedRuns(tb, line, classifier, formatMap, foreground);

                int targetLine = line.LineNumber;
                tb.MouseLeftButtonUp += (s, e2) => ScrollToLine(targetLine);
                tb.MouseEnter += (s, e2) =>
                {
                    var hover = new SolidColorBrush(Color.FromArgb(48, 128, 128, 128));
                    hover.Freeze();
                    tb.Background = hover;
                };
                tb.MouseLeave += (s, e2) => tb.Background = null;

                _root.Children.Add(tb);

                if (i < lines.Count - 1)
                {
                    // 粘滞行之间的细分隔线（比编辑器网格线略深）
                    var sep = new Border
                    {
                        Height = 1,
                        Background = DeriveSeparatorBrush(bgBrush ?? Brushes.White)
                    };
                    _root.Children.Add(sep);
                }
            }

            // 粘滞栏与代码区之间的底部分隔线
            _root.Children.Add(new Border
            {
                Height = 1,
                Background = DeriveSeparatorBrush(bgBrush ?? Brushes.White)
            });
        }

        /// <summary>
        /// 用分类器对行文本着色，追加 Runs 到 TextBlock；无分类结果时用纯前景色。
        /// </summary>
        private void AppendClassifiedRuns(TextBlock tb, StickyLine line, IClassifier classifier,
            IClassificationFormatMap formatMap, Brush fallbackForeground)
        {
            if (classifier == null || formatMap == null)
            {
                tb.Text = line.Text;
                tb.Foreground = fallbackForeground;
                return;
            }

            try
            {
                var snapshot = _view.TextSnapshot;
                if (line.LineNumber >= snapshot.LineCount)
                {
                    tb.Text = line.Text;
                    tb.Foreground = fallbackForeground;
                    return;
                }
                var snapshotLine = snapshot.GetLineFromLineNumber(line.LineNumber);
                var spans = classifier.GetClassificationSpans(snapshotLine.Extent);

                if (spans == null || spans.Count == 0)
                {
                    tb.Text = line.Text;
                    tb.Foreground = fallbackForeground;
                    return;
                }

                int pos = 0;
                foreach (var span in spans.OrderBy(s => s.Span.Start.Position))
                {
                    int start = span.Span.Start.Position - snapshotLine.Start.Position;
                    int length = span.Span.Length;
                    if (length <= 0)
                        continue;

                    if (start > pos)
                        tb.Inlines.Add(new Run(line.Text.Substring(pos, start - pos)) { Foreground = fallbackForeground });

                    var props = formatMap.GetTextProperties(span.ClassificationType);
                    var run = new Run(line.Text.Substring(start, length))
                    {
                        Foreground = props.ForegroundBrush ?? fallbackForeground
                    };
                    if (props.Bold)
                        run.FontWeight = FontWeights.Bold;
                    tb.Inlines.Add(run);

                    pos = start + length;
                }

                if (pos < line.Text.Length)
                    tb.Inlines.Add(new Run(line.Text.Substring(pos)) { Foreground = fallbackForeground });
            }
            catch
            {
                // 分类失败时退化为纯文本
                tb.Text = line.Text;
                tb.Foreground = fallbackForeground;
            }
        }

        /// <summary>
        /// 根据编辑器背景色派生分隔线颜色（背景暗则亮线，背景亮则暗线）。
        /// </summary>
        private static Brush DeriveSeparatorBrush(SolidColorBrush background)
        {
            var c = background.Color;
            byte target = (byte)((c.R + c.G + c.B) / 3 > 128 ? 60 : 220);
            var brush = new SolidColorBrush(Color.FromRgb(target, target, target));
            brush.Freeze();
            return brush;
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
