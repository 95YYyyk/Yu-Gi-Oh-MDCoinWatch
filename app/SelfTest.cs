namespace MdCoinWatch;

/// <summary>离线验证：拿目录里的截图跑一遍全部判据，用的就是实时识别那份代码。</summary>
internal static class SelfTest
{
    /// <summary>--size 指定要模拟的客户区尺寸；不指定就按 1920x1080 基准。</summary>
    internal static (int w, int h) ParseSize(Options o)
    {
        if (o.TestSize.Length == 0) return (Geometry.RefW, Geometry.RefH);
        var parts = o.TestSize.ToLowerInvariant().Split('x');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out int w) || !int.TryParse(parts[1], out int h)
            || w < 320 || h < 240)
            throw new ArgumentException("--size 的格式应该是 1600x900");
        return (w, h);
    }

    internal static int Run(string dir, Options o)
    {
        if (!Directory.Exists(dir)) { Console.WriteLine("目录不存在: " + dir); return 1; }

        var (tw, th) = ParseSize(o);
        double fx = (double)tw / Geometry.RefW, fy = (double)th / Geometry.RefH;
        var ts = TemplateSet.Load().Scaled(fx, fy);

        Console.WriteLine("模拟客户区 {0}x{1}   缩放 x{2:0.0000} y{3:0.0000}", tw, th, fx, fy);
        Console.WriteLine("模板  kw_first {0}x{1}  kw_second {2}x{3}  choice_line {4}x{5}  wait_line {6}x{7}  end_row {8}x{9}",
            ts.KwFirst.W, ts.KwFirst.H, ts.KwSecond.W, ts.KwSecond.H,
            ts.ChoiceLine.W, ts.ChoiceLine.H, ts.WaitLine.W, ts.WaitLine.H, ts.EndRow.W, ts.EndRow.H);
        Console.WriteLine("阈值  match>={0:0.00}  turn>={1:0.00}  yellow>={2}", o.MatchThreshold, o.TurnThreshold, o.ButtonYellowMin);

        var files = Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly)
                             .OrderBy(f => f, StringComparer.Ordinal).ToArray();
        if (files.Length == 0) { Console.WriteLine("目录里没有 PNG。"); return 1; }

        Console.WriteLine();
        Console.WriteLine("{0,-30}{1,8}{2,8}{3,9}{4,9}{5,9}{6,7}{7,7}   {8}",
            "文件", "先攻", "后攻", "选择行", "等待行", "结束行", "黄A", "黄B", "判定");
        Console.WriteLine(new string('-', 106));

        double[] bufA = Array.Empty<double>(), bufB = Array.Empty<double>();
        foreach (var f in files)
        {
            byte[] rgb; int w, h;
            try { (rgb, w, h) = Png.Load(f); }
            catch (Exception e)
            {
                Console.WriteLine(string.Format("{0,-30} 解码失败: {1}", Short(Path.GetFileName(f)), e.Message));
                continue;
            }
            rgb = Png.Resize(rgb, w, h, tw, th);
            w = tw; h = th;

            int yA = CropYellow(rgb, w, h, Geometry.ScaledBox(Geometry.BtnFirst, fx, fy, 0));
            int yB = CropYellow(rgb, w, h, Geometry.ScaledBox(Geometry.BtnSecond, fx, fy, 0));
            double scC = CropScore(rgb, w, h, Geometry.ScaledBox(Geometry.ChoiceLine, fx, fy, Geometry.Margin), ts.ChoiceLine, ref bufA, ref bufB);
            double scW = CropScore(rgb, w, h, Geometry.ScaledBox(Geometry.WaitLine, fx, fy, Geometry.Margin), ts.WaitLine, ref bufA, ref bufB);
            double scE = CropScore(rgb, w, h, Geometry.ScaledBox(Geometry.EndRow, fx, fy, Geometry.Margin), ts.EndRow, ref bufA, ref bufB);
            double scF = CropScore(rgb, w, h, Geometry.ScaledBox(Geometry.KwBox, fx, fy, Geometry.Margin), ts.KwFirst, ref bufA, ref bufB);
            double scS = CropScore(rgb, w, h, Geometry.ScaledBox(Geometry.KwBox, fx, fy, Geometry.Margin), ts.KwSecond, ref bufA, ref bufB);

            var coin = Core.DecideCoin(yA, yB, scC, scW, o);
            var turn = Core.DecideTurn(scF, scS, o);
            bool end = scE >= o.MatchThreshold;

            var v = new List<string>();
            if (coin == Coin.Win) v.Add("投币=胜");
            if (coin == Coin.Lose) v.Add("投币=负");
            if (turn == Turn.First) v.Add("先攻");
            if (turn == Turn.Second) v.Add("后攻");
            if (end) v.Add("对局结束");
            if (v.Count == 0) v.Add("-");

            Console.WriteLine("{0,-30}{1,8:0.000}{2,8:0.000}{3,9:0.000}{4,9:0.000}{5,9:0.000}{6,7}{7,7}   {8}",
                Short(Path.GetFileName(f)), scF, scS, scC, scW, scE, yA, yB, string.Join(" ", v));
        }
        return 0;
    }

    /// <summary>
    /// 用样本截图按真实先后顺序喂给状态机，走的判定和记录代码与实时运行完全一致。
    /// 用来验证「投币 -> 先后手 -> 对局结束 -> 写入 CSV」整条链路。
    /// </summary>
    internal static int Replay(string dir, Options o)
    {
        if (!Directory.Exists(dir)) { Console.WriteLine("目录不存在: " + dir); return 1; }

        var script = new[]
        {
            "匹配界面.png",
            "我方选择先后手.png",
            "我方先手.png",
            "后攻进入游戏后的画面.png",
            "结束失败画面.png",
            "匹配界面.png",
            "反面后对手选择先后手.png",
            "我方后攻.png",
            "后攻进入游戏后的画面.png",
            "结束失败画面.png",
        };

        var (tw, th) = ParseSize(o);
        double fx = (double)tw / Geometry.RefW, fy = (double)th / Geometry.RefH;
        var ts = TemplateSet.Load().Scaled(fx, fy);

        var rec = new Recorder(o.Csv);
        var engine = new Engine(o, rec, m => Console.WriteLine("    " + m));
        Console.WriteLine("回放目录: " + Path.GetFullPath(dir));
        Console.WriteLine("模拟客户区 {0}x{1}   缩放 x{2:0.0000} y{3:0.0000}", tw, th, fx, fy);
        Console.WriteLine("CSV:      " + rec.FilePath);
        Console.WriteLine();

        double[] bufA = Array.Empty<double>(), bufB = Array.Empty<double>();
        var now = new DateTime(2026, 10, 2, 21, 0, 0);

        foreach (var name in script)
        {
            var path = Path.Combine(dir, name);
            if (!File.Exists(path))
            {
                var alt = Directory.GetFiles(dir, name, SearchOption.AllDirectories).FirstOrDefault();
                if (alt == null) { Console.WriteLine("  [跳过] 找不到 " + name); now = now.AddSeconds(6); continue; }
                path = alt;
            }

            byte[] rgb; int w, h;
            try { (rgb, w, h) = Png.Load(path); }
            catch (Exception e) { Console.WriteLine("  [跳过] " + name + " 解码失败: " + e.Message); now = now.AddSeconds(6); continue; }
            rgb = Png.Resize(rgb, w, h, tw, th);
            w = tw; h = th;

            int yA = CropYellow(rgb, w, h, Geometry.ScaledBox(Geometry.BtnFirst, fx, fy, 0));
            int yB = CropYellow(rgb, w, h, Geometry.ScaledBox(Geometry.BtnSecond, fx, fy, 0));
            double scC = CropScore(rgb, w, h, Geometry.ScaledBox(Geometry.ChoiceLine, fx, fy, Geometry.Margin), ts.ChoiceLine, ref bufA, ref bufB);
            double scW = CropScore(rgb, w, h, Geometry.ScaledBox(Geometry.WaitLine, fx, fy, Geometry.Margin), ts.WaitLine, ref bufA, ref bufB);
            double scE = CropScore(rgb, w, h, Geometry.ScaledBox(Geometry.EndRow, fx, fy, Geometry.Margin), ts.EndRow, ref bufA, ref bufB);
            double scF = CropScore(rgb, w, h, Geometry.ScaledBox(Geometry.KwBox, fx, fy, Geometry.Margin), ts.KwFirst, ref bufA, ref bufB);
            double scS = CropScore(rgb, w, h, Geometry.ScaledBox(Geometry.KwBox, fx, fy, Geometry.Margin), ts.KwSecond, ref bufA, ref bufB);

            var coin = Core.DecideCoin(yA, yB, scC, scW, o);
            var turn = Core.DecideTurn(scF, scS, o);
            bool end = scE >= o.MatchThreshold;

            Console.WriteLine(string.Format("  {0:HH:mm:ss}  {1,-24} 投币={2,-4} 先后手={3,-6} 结束={4}",
                now, name, Core.CoinText(coin), Core.TurnText(turn), end ? "是" : "否"));

            if (engine.State == 2) { if (end) engine.OnEnd(now); }
            else if (engine.State == 1) { engine.OnTurn(turn, now); if (engine.State == 1) engine.OnCoin(coin, now); }
            else engine.OnCoin(coin, now);
            engine.Tick(now);

            now = now.AddSeconds(6);
        }

        Console.WriteLine();
        Console.WriteLine("共写入 " + engine.Records + " 条记录 -> " + rec.FilePath);
        return engine.Records == 2 ? 0 : 1;
    }

    private static string Short(string s) => s.Length <= 29 ? s : s.Substring(0, 29);

    private static byte[] CropBgra(byte[] rgb, int w, int h, (int x, int y, int w, int h) r)
    {
        int cw = r.w, ch = r.h;
        var b = new byte[cw * ch * 4];
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
            {
                int sx = Math.Clamp(r.x + x, 0, w - 1);
                int sy = Math.Clamp(r.y + y, 0, h - 1);
                int p = (sy * w + sx) * 3, d = (y * cw + x) * 4;
                b[d] = rgb[p + 2];
                b[d + 1] = rgb[p + 1];
                b[d + 2] = rgb[p];
                b[d + 3] = 255;
            }
        return b;
    }

    private static int CropYellow(byte[] rgb, int w, int h, (int x, int y, int w, int h) r)
        => Core.CountYellow(CropBgra(rgb, w, h, r), 190, 150, 140, 80);

    private static double CropScore(byte[] rgb, int w, int h, (int x, int y, int w, int h) r,
                                    Template t, ref double[] bufA, ref double[] bufB)
    {
        var bgra = CropBgra(rgb, w, h, r);
        var g = new byte[r.w * r.h];
        for (int i = 0, p = 0; i < g.Length; i++, p += 4)
            g[i] = (byte)((bgra[p + 2] * 299 + bgra[p + 1] * 587 + bgra[p] * 114) / 1000);
        return Core.Ncc(g, r.w, r.h, t, Geometry.SearchStep, ref bufA, ref bufB, out _, out _);
    }
}
