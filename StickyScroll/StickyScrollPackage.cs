using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace StickyScroll
{
    /// <summary>
    /// 扩展包：承载选项页注册（工具 → 选项 → StickyScroll）。
    /// 通过 pkgdef 注册（VSIX 内附带 StickyScroll.pkgdef）。
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [Guid(StickyScrollPackage.PackageGuidString)]
    [ProvideOptionPage(typeof(StickyScrollOptions), "StickyScroll", "General", 0, 0, true)]
    public sealed class StickyScrollPackage : Package
    {
        public const string PackageGuidString = "8C3F0D52-4E6B-4A9C-B2D3-5F7A1C9E3B04";
    }
}
