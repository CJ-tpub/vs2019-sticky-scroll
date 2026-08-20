using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace StickyScroll
{
    /// <summary>
    /// 选项页：工具 → 选项 → StickyScroll → General。
    /// 属性自动持久化到 VS 注册表（标准 DialogPage 机制），修改后实时生效。
    /// </summary>
    [Guid(StickyScrollOptions.PageGuidString)]
    public class StickyScrollOptions : DialogPage
    {
        public const string PageGuidString = "7B2E9C41-3D5A-4F8B-9C1E-2A6B4D8F0A12";

        /// <summary>当前选项实例（Margin 渲染时读取，实时生效）。</summary>
        public static StickyScrollOptions Instance { get; private set; }

        public StickyScrollOptions()
        {
            Instance = this;
        }

        [Category("General")]
        [DisplayName("Max sticky lines")]
        [Description("Number of scope-header lines pinned at the top of the editor (1-10).")]
        [DefaultValue(3)]
        public int MaxLines { get; set; } = 3;

        [Category("General")]
        [DisplayName("Enabled")]
        [Description("Enable or disable sticky scroll entirely.")]
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;
    }
}
