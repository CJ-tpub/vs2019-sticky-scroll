using System;
using System.IO;

namespace StickyScroll
{
    /// <summary>
    /// 配置文件设置（%APPDATA%\StickyScroll\settings.ini）。
    /// 修改文件保存后，滚动编辑器即生效（按文件时间戳自动重载）。
    /// 格式：
    ///   MaxLines=5     (1-10)
    ///   Enabled=true   (true/false)
    /// </summary>
    internal static class StickyScrollSettings
    {
        private const int DefaultMaxLines = 3;

        private static string FilePath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "StickyScroll", "settings.ini");
            }
        }

        private static DateTime _lastWrite = DateTime.MinValue;
        private static int _maxLines = DefaultMaxLines;
        private static bool _enabled = true;

        public static int MaxLines
        {
            get { ReloadIfChanged(); return _maxLines; }
        }

        public static bool Enabled
        {
            get { ReloadIfChanged(); return _enabled; }
        }

        private static void ReloadIfChanged()
        {
            try
            {
                var fi = new FileInfo(FilePath);
                if (!fi.Exists)
                {
                    _maxLines = DefaultMaxLines;
                    _enabled = true;
                    _lastWrite = DateTime.MinValue;
                    return;
                }
                if (fi.LastWriteTime == _lastWrite)
                    return;
                _lastWrite = fi.LastWriteTime;

                int maxLines = DefaultMaxLines;
                bool enabled = true;
                foreach (var rawLine in File.ReadAllLines(FilePath))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";"))
                        continue;
                    var eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;
                    var key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    var value = line.Substring(eq + 1).Trim();
                    if (key == "maxlines")
                    {
                        int n;
                        if (int.TryParse(value, out n) && n >= 1 && n <= 10)
                            maxLines = n;
                    }
                    else if (key == "enabled")
                    {
                        bool b;
                        if (bool.TryParse(value, out b))
                            enabled = b;
                    }
                }
                _maxLines = maxLines;
                _enabled = enabled;
            }
            catch
            {
                // 读取失败时保持默认/上次值
            }
        }
    }
}
