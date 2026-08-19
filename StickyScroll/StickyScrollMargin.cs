using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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
    /// 行为对齐 VS2022/VSCode：滚动时把 namespace/class/method 等声明行钉在顶部，点击可跳转。
    /// </summary>
    internal sealed class StickyScrollMargin : IWpfTextViewMargin
    {
        public const string MarginName = "StickyScrollMargin";

        // 默认显示的最大粘滞行数（后续由选项页接管）
        private const int DefaultMaxLines = 3;
        private const double BackgroundOpacity = 0.94;

        private readonly IWpfTextView _view;
        private readonly StickyLineProvider _stickyLineProvider;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly StackPanel _root;
        private bool _isDisposed;

        // 最近一次渲染的链（用于避免无谓重绘）
        private IList<StickyLine> _lastLines = new StickyLine[0];

        public StickyScrollMargin(IWpfTextViewHost textViewHost, StickyLineProvider stickyLineProvider, IEditorFormatMap editorFormatMap)
        {
            _view = textViewHost.TextView;
            _stickyLineProvider = stickyLineProvider;
            _editorFormatMap = editorFormatMap;

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
            // 文本变化：下一次 LayoutChanged 也会触发，这里直接刷新保证即时性
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

        private void Render(IList<StickyLine> lines)
        {
            _root.Children.Clear();

            if (lines.Count == 0)
                return;

            // 主题颜色（Plain Text 的前景/背景）
            var foreground = GetBrush(EditorFormatDefinition.ForegroundBrushId, Brushes.Gray);
            var background = GetBrush(EditorFormatDefinition.BackgroundBrushId, Brushes.White);

            // 字体（与编辑器一致，含缩放）
            Typeface typeface = _view.FormattedLineSource != null
                ? _view.FormattedLineSource.DefaultTextProperties.Typeface
                : new Typeface("Consolas");
            double fontSize = _view.FormattedLineSource != null
                ? _view.FormattedLineSource.DefaultTextProperties.FontRenderingEmSize
                : 14.0;

            var bgColor = background is SolidColorBrush solid ? solid.Color : Colors.White;
            var bgBrush = new SolidColorBrush(Color.FromArgb(
                (byte)(BackgroundOpacity * 255), bgColor.R, bgColor.G, bgColor.B));
            bgBrush.Freeze();
            _root.Background = bgBrush;

            double lineHeight = _view.LineHeight > 0 ? _view.LineHeight : fontSize * 1.4;
            double separatorColor = 0.5;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var tb = new TextBlock
                {
                    Text = line.Text,
                    FontFamily = typeface.FontFamily,
                    FontSize = fontSize,
                    FontStyle = typeface.Style,
                    FontWeight = typeface.Weight,
                    Foreground = foreground,
                    Margin = new Thickness(8 + line.IndentLength * 0.0, 0, 8, 0), // 简化缩进展示
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = "Sticky scroll: " + line.Text,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Cursor = Cursors.Hand
                };
                if (i > 0)
                {
                    // 行间细分割线
                    var sep = new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush(Color.FromRgb(
                            (byte)(bgColor.R * separatorColor + 128 * (1 - separatorColor)),
                            (byte)(bgColor.G * separatorColor + 128 * (1 - separatorColor)),
                            (byte)(bgColor.B * separatorColor + 128 * (1 - separatorColor))))
                    };
                    _root.Children.Add(sep);
                }

                int targetLine = line.LineNumber;
                tb.MouseLeftButtonUp += (s, e2) => ScrollToLine(targetLine);
                tb.MouseEnter += (s, e2) =>
                {
                    var hover = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128));
                    hover.Freeze();
                    tb.Background = hover;
                };
                tb.MouseLeave += (s, e2) => tb.Background = null;

                _root.Children.Add(tb);
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
