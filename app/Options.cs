namespace MdCoinWatch;

internal sealed class Options
{
    internal string Process = "masterduel";
    internal string Csv = "duel_stats.csv";
    internal int Fps = 10;
    internal bool RequireForeground = true;
    internal bool Verbose;
    internal bool List;
    internal string DumpDir = "";
    internal string SelfTestDir = "";
    internal string ReplayDir = "";
    internal string TestSize = "";
    internal double MatchThreshold = 0.85;
    internal double TurnThreshold = 0.86;
    internal int ButtonYellowMin = 40;
    internal bool Help;

    internal static Options Parse(string[] a)
    {
        var o = new Options();
        foreach (var raw in a)
        {
            var s = raw.Trim();
            if (s.Length == 0) continue;
            var key = s;
            var val = "";
            var eq = s.IndexOf('=');
            if (eq > 0) { key = s.Substring(0, eq); val = s.Substring(eq + 1); }

            switch (key.ToLowerInvariant())
            {
                case "--process": case "--window": o.Process = val; break;
                case "--csv": o.Csv = val; break;
                case "--fps": o.Fps = Math.Clamp(int.TryParse(val, out var f) ? f : 10, 1, 60); break;
                case "--threshold": o.MatchThreshold = double.TryParse(val, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : 0.85; break;
                case "--turn-threshold": o.TurnThreshold = double.TryParse(val, System.Globalization.CultureInfo.InvariantCulture, out var tt) ? tt : 0.86; break;
                case "--any-focus": o.RequireForeground = false; break;
                case "--verbose": case "-v": o.Verbose = true; break;
                case "--list": o.List = true; break;
                case "--dump": o.DumpDir = val; break;
                case "--selftest": o.SelfTestDir = val; break;
                case "--replay": o.ReplayDir = val; break;
                case "--size": o.TestSize = val; break;
                case "--help": case "-h": case "/?": o.Help = true; break;
                default:
                    Console.WriteLine("[warn] 未知参数，已忽略: " + raw);
                    break;
            }
        }
        return o;
    }

    internal void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("MdCoinWatch - 大师决斗 投币胜负 / 先后手 记录器");
        Console.WriteLine();
        Console.WriteLine("用法: MdCoinWatch [选项]");
        Console.WriteLine();
        Console.WriteLine("  --process=名字    游戏进程名前缀，默认 masterduel");
        Console.WriteLine("  --csv=路径        输出 CSV，默认 exe 同目录 duel_stats.csv");
        Console.WriteLine("  --fps=10          每秒抓帧次数，默认 10");
        Console.WriteLine("  --any-focus       游戏不在前台时也继续识别（默认暂停）");
        Console.WriteLine("  --dump=目录       发生状态变化时把抓到的区域存成 BMP，便于排错");
        Console.WriteLine("  --selftest=目录   离线跑指定目录里的截图，打印全部匹配分数");
        Console.WriteLine("  --replay=目录     用带时间顺序的样本截图跑一遍状态机，验证记录链路");
        Console.WriteLine("  --size=1600x900   selftest / replay 按这个客户区尺寸缩放后再判，验证别的分辨率");
        Console.WriteLine("  --list            列出当前所有可见窗口及进程名");
        Console.WriteLine("  --verbose         打印每一帧的分数");
        Console.WriteLine("  --help            显示这段说明");
    }
}
