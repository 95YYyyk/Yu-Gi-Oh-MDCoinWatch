using System.Globalization;

namespace MdCoinWatch;

/// <summary>简单 key=value 配置，放在 exe 旁边，手改也方便。不用 JSON 是为了裁剪发布时不出反射问题。</summary>
internal sealed class Settings
{
    internal string TextColor = "#EEF2F8";
    internal string AccentColor = "#FFC53D";
    internal string DimColor = "#7C8496";
    internal string PanelColor = "#0E1015";
    internal int Opacity = 92;
    internal int X = int.MinValue;
    internal int Y = int.MinValue;
    internal string Csv = "duel_stats.csv";
    internal string Process = "masterduel";
    internal int Fps = 10;
    internal int Width = 300;      // 浮窗逻辑宽度，高度按比例算

    internal static string FilePath => Path.Combine(AppContext.BaseDirectory, "YuGiOh-MDCoinWatch.ini");

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
                    case "panelcolor": s.PanelColor = v; break;
                    case "opacity": if (int.TryParse(v, out var o)) s.Opacity = Math.Clamp(o, 25, 100); break;
                    case "x": if (int.TryParse(v, out var x)) s.X = x; break;
                    case "y": if (int.TryParse(v, out var y)) s.Y = y; break;
                    case "csv": s.Csv = v; break;
                    case "process": s.Process = v; break;
                    case "fps": if (int.TryParse(v, out var fp)) s.Fps = Math.Clamp(fp, 2, 60); break;
                    case "width": if (int.TryParse(v, out var wd)) s.Width = Math.Clamp(wd, 240, 720); break;
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
                "panelColor=" + PanelColor,
                "opacity=" + Opacity.ToString(CultureInfo.InvariantCulture),
                "# 浮窗位置。-1 表示没设过，会自动放到右上角。",
                "x=" + X.ToString(CultureInfo.InvariantCulture),
                "y=" + Y.ToString(CultureInfo.InvariantCulture),
                "# 数据与游戏",
                "csv=" + Csv,
                "process=" + Process,
                "fps=" + Fps.ToString(CultureInfo.InvariantCulture),
                "# 浮窗宽度 240~720，高度按比例。也可以直接用右键菜单里的「大小」。",
                "width=" + Width.ToString(CultureInfo.InvariantCulture),
                ""
            };
            File.WriteAllText(FilePath, string.Join(Environment.NewLine, lines));
        }
        catch { }
    }
}
