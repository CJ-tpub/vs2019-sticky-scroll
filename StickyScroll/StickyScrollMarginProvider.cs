using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.Utilities;

namespace StickyScroll
{
    /// <summary>
    /// 粘滞滚动条 margin 的 MEF 提供者：注册为顶部 margin，仅用于文档视图。
    /// </summary>
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(StickyScrollMargin.MarginName)]
    [Order(After = PredefinedMarginNames.Top)]
    [MarginContainer(PredefinedMarginNames.Top)]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class StickyScrollMarginProvider : IWpfTextViewMarginProvider
    {
        [Import]
        internal IOutliningManagerService OutliningManagerService { get; set; }

        [Import]
        internal IEditorFormatMapService EditorFormatMapService { get; set; }

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost textViewHost, IWpfTextViewMargin containerMargin)
        {
            var formatMap = EditorFormatMapService.GetEditorFormatMap(textViewHost.TextView);
            return new StickyScrollMargin(textViewHost, new StickyLineProvider(OutliningManagerService), formatMap);
        }
    }
}
