using System.Globalization;

namespace MdCoinWatch;

/// <summary>简单 key=value 配置，放在 exe 旁边，手改也方便。不用 JSON 是为了裁剪发布时不出反射问题。</summary>
internal sealed class Settings
{
    internal string TextColor = "#EEF2F8";
    internal string AccentColor = "#FFC53D";
    internal string DimColor = "#7C8496";
    internal string RateColor = "#5FD3C4";     // 胜率单独用的突出色
    internal string PanelColor = "#0E1015";
    internal int Opacity = 92;
    internal int X = int.MinValue;
    internal int Y = int.MinValue;
    internal string Csv = "duel_stats.csv";
    internal string Process = "masterduel";
    internal int Fps = 10;
    internal int Width = 248;      // 浮窗逻辑宽度，高度按比例算
    internal bool ShowInTaskbar;   // 是否在任务栏显示（OBS 的「窗口采集」要靠它才列得出本窗口）

    // 显示哪些统计信息（右键「显示项目」里勾）
    internal bool ShowCoin = true;
    internal bool ShowTurn = true;
    internal bool ShowResult = true;
    internal bool ShowRate = true;
    internal bool ShowFortune = true;

    internal static string FilePath => Path.Combine(AppContext.BaseDirectory, "YuGiOh-MDCoinWatch.ini");

    /// <summary>CSV 的绝对路径。程序里所有地方都用它，别各算各的。</summary>
    internal string CsvPath => Path.IsPathRooted(Csv) ? Csv : Path.Combine(AppContext.BaseDirectory, Csv);

    internal static Settings Load()
    {
        var s = new Settings();
        try
        {
            if (!File.Exists(FilePath)) { s.Save(); return s; }
            foreach (var raw in File.ReadAllLines(FilePath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var k = line.Substring(0, eq).Trim().ToLowerInvariant();
                var v = line.Substring(eq + 1).Trim();
                switch (k)
                {
                    case "textcolor": s.TextColor = v; break;
                    case "accentcolor": s.AccentColor = v; break;
                    case "dimcolor": s.DimColor = v; break;
                    case "ratecolor": s.RateColor = v; break;
                    case "panelcolor": s.PanelColor = v; break;
                    case "opacity": if (int.TryParse(v, out var o)) s.Opacity = Math.Clamp(o, 25, 100); break;
                    case "x": if (int.TryParse(v, out var x)) s.X = x; break;
                    case "y": if (int.TryParse(v, out var y)) s.Y = y; break;
                    case "csv": s.Csv = v; break;
                    case "process": s.Process = v; break;
                    case "fps": if (int.TryParse(v, out var fp)) s.Fps = Math.Clamp(fp, 2, 60); break;
                    case "width": if (int.TryParse(v, out var wd)) s.Width = Math.Clamp(wd, 200, 720); break;
                    case "showintaskbar": s.ShowInTaskbar = v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case "showcoin": s.ShowCoin = v != "0"; break;
                    case "showturn": s.ShowTurn = v != "0"; break;
                    case "showresult": s.ShowResult = v != "0"; break;
                    case "showrate": s.ShowRate = v != "0"; break;
                    case "showfortune": s.ShowFortune = v != "0"; break;
                }
            }
        }
        catch { }
        return s;
    }

    internal void Save()
    {
        try
        {
            var lines = new[]
            {
                "# Yu-Gi-Oh MDCoinWatch 配置。改完保存，重启程序生效。",
                "#",
                "# 颜色都写 #RRGGBB。textcolor 正文字色，accentcolor 百分比和运势的字色，",
                "# dimcolor 次要文字色，panelcolor 面板底色，opacity 面板不透明度 25~100。",
                "textColor=" + TextColor,
                "accentColor=" + AccentColor,
                "dimColor=" + DimColor,
                "rateColor=" + RateColor,
                "panelColor=" + PanelColor,
                "opacity=" + Opacity.ToString(CultureInfo.InvariantCulture),
                "# 浮窗位置。-1 表示没设过，会自动放到右上角。",
                "x=" + X.ToString(CultureInfo.InvariantCulture),
                "y=" + Y.ToString(CultureInfo.InvariantCulture),
                "# 数据与游戏",
                "csv=" + Csv,
                "process=" + Process,
                "fps=" + Fps.ToString(CultureInfo.InvariantCulture),
                "# 浮窗宽度 200~720，高度按比例。也可以直接拖浮窗右下角。",
                "width=" + Width.ToString(CultureInfo.InvariantCulture),
                "# 1 = 显示在任务栏，0 = 不显示。OBS 的「窗口采集」需要 1 才能看到本窗口。",
                "showInTaskbar=" + (ShowInTaskbar ? "1" : "0"),
                "# 显示哪些统计信息。1 = 显示，0 = 隐藏。右键菜单里的「显示项目」也能改。",
                "showCoin=" + (ShowCoin ? "1" : "0"),
                "showTurn=" + (ShowTurn ? "1" : "0"),
                "showResult=" + (ShowResult ? "1" : "0"),
                "showRate=" + (ShowRate ? "1" : "0"),
                "showFortune=" + (ShowFortune ? "1" : "0"),
                ""
            };
            File.WriteAllText(FilePath, string.Join(Environment.NewLine, lines));
        }
        catch { }
    }
}
