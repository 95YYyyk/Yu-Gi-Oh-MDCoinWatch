using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace MdCoinWatch;

internal static class Program
{
    private const string Version = "1.1";

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [STAThread]
    private static int Main(string[] args)
    {
        var o = Options.Parse(args);
        bool console = o.List || o.Help || o.SelfTestDir.Length > 0 || o.ReplayDir.Length > 0;

        if (console)
        {
            // 这是个 WinExe，本来没有控制台。挂到调用它的那个控制台上，命令行模式才有输出。
            if (AttachConsole(-1))
            {
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true });
                Log.Echo = true;
            }
            Win32.EnableDpiAwareness();
            try
            {
                if (o.Help) { o.PrintHelp(); return 0; }
                if (o.List) return ListWindows();
                if (o.SelfTestDir.Length > 0) return SelfTest.Run(o.SelfTestDir, o);
                return SelfTest.Replay(o.ReplayDir, o);
            }
            catch (Exception e) { Console.WriteLine("[错误] " + e.Message); return 1; }
        }

        Win32.EnableDpiAwareness();

        using var mutex = new Mutex(true, "Local\\YuGiOh-MDCoinWatch.SingleInstance", out bool isNew);
        if (!isNew)
        {
            Ui32.MessageBoxW(nint.Zero, "Yu-Gi-Oh MDCoinWatch 已经在运行了（看屏幕右上角）。", "Yu-Gi-Oh MDCoinWatch", 0x40);
            return 0;
        }

        Settings cfg;
        try { cfg = Settings.Load(); }
        catch { cfg = new Settings(); }

        Log.Init(Path.Combine(AppContext.BaseDirectory, "YuGiOh-MDCoinWatch.log"));
        Log.Write("启动 v" + Version);

        var csv = Path.IsPathRooted(cfg.Csv) ? cfg.Csv : Path.Combine(AppContext.BaseDirectory, cfg.Csv);
        var stats = new Stats();
        stats.LoadCsv(csv);
        Log.Write("今日已有 " + stats.Read().Total + " 条记录   CSV=" + csv);

        var worker = new Thread(() => DetectLoop(stats, cfg, csv)) { IsBackground = true, Name = "detect" };
        worker.Start();

        Widget? widget = null;
        try
        {
            widget = new Widget(stats, cfg);
            while (true)
            {
                int r = Ui32.GetMessageW(out var msg, nint.Zero, 0, 0);
                if (r <= 0) break;                       // 0 = WM_QUIT, -1 = 出错
                Ui32.TranslateMessage(ref msg);
                Ui32.DispatchMessageW(ref msg);
            }
        }
        catch (Exception e)
        {
            Log.Write("界面崩了: " + e);
            Ui32.MessageBoxW(nint.Zero, e.ToString(), "Yu-Gi-Oh MDCoinWatch 出错", 0x10);
        }
        finally { widget?.Dispose(); }
        Log.Write("退出");
        return 0;
    }

    private static int ListWindows()
    {
        Console.WriteLine("{0,-14}{1,-12}{2,12}   {3}", "进程", "句柄", "客户区", "标题");
        Win32.EnumWindows((h, _) =>
        {
            if (!Win32.IsWindowVisible(h) || Win32.IsIconic(h)) return true;
            Win32.GetWindowThreadProcessId(h, out uint pid);
            var name = Win32.ProcessName(pid);
            if (name.Length == 0) return true;
            if (!Win32.GetClientRect(h, out var r)) return true;
            int w = r.Right - r.Left, hh = r.Bottom - r.Top;
            if (w < 200 || hh < 150) return true;
            var title = Win32.WindowTitle(h);
            Console.WriteLine("{0,-14}{1,-12}{2,12}   {3}", name, h.ToString("X"),
                w + "x" + hh, title.Length > 70 ? title.Substring(0, 70) : title);
            return true;
        }, IntPtr.Zero);
        return 0;
    }

    private static IntPtr FindWindow(string prefix, out string title)
    {
        IntPtr found = IntPtr.Zero;
        int best = 0;
        string t = "";
        Win32.EnumWindows((h, _) =>
        {
            if (!Win32.IsWindowVisible(h) || Win32.IsIconic(h)) return true;
            Win32.GetWindowThreadProcessId(h, out uint pid);
            if (pid == 0) return true;
            var name = Win32.ProcessName(pid);
            if (name.Length == 0 || !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            if (!Win32.GetClientRect(h, out var r)) return true;
            int w = r.Right - r.Left, hh = r.Bottom - r.Top;
            if (w < 400 || hh < 300) return true;
            if (w * hh > best) { best = w * hh; found = h; t = Win32.WindowTitle(h); }
            return true;
        }, IntPtr.Zero);
        title = t;
        return found;
    }

    private static void DetectLoop(Stats stats, Settings cfg, string csvPath)
    {
        try
        {
            var screenDc = Win32.GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero) { Log.Write("取屏幕 DC 失败"); stats.SetStatus("出错了，看日志"); return; }

            var baseTs = TemplateSet.Load();
            var rec = new Recorder(csvPath);
            var o = new Options { Process = cfg.Process, Csv = csvPath, Fps = cfg.Fps, RequireForeground = true };
            var engine = new Engine(o, rec, Log.Write);
            engine.OnRecorded += (c, t) => stats.AddRecord(Core.CoinText(c), Core.TurnText(t));

            int poll = Math.Max(1, 1000 / cfg.Fps);
            IntPtr hwnd = IntPtr.Zero;
            Detector? det = null;
            double sx = 0, sy = 0;
            string title = "";
            string lastStatus = "";

            void Status(string s)
            {
                if (s == lastStatus) return;
                lastStatus = s;
                stats.SetStatus(s, title);
            }

            while (true)
            {
                if (hwnd == IntPtr.Zero)
                {
                    hwnd = FindWindow(cfg.Process, out title);
                    if (hwnd == IntPtr.Zero) { Status("等待游戏"); Thread.Sleep(2000); continue; }
                    Log.Write("锁定窗口: " + title);
                }

                if (!Win32.GetClientRect(hwnd, out var rc) || rc.Right - rc.Left < 400 || rc.Bottom - rc.Top < 300)
                {
                    hwnd = IntPtr.Zero;
                    continue;
                }
                int cw = rc.Right - rc.Left, chh = rc.Bottom - rc.Top;

                if (Win32.GetForegroundWindow() != hwnd)
                {
                    Status("切回游戏");
                    Thread.Sleep(400);
                    continue;
                }

                double nsx = (double)cw / Geometry.RefW, nsy = (double)chh / Geometry.RefH;
                if (det == null || Math.Abs(nsx - sx) > 1e-9 || Math.Abs(nsy - sy) > 1e-9)
                {
                    det?.Dispose();
                    sx = nsx; sy = nsy;
                    det = new Detector(screenDc, baseTs.Scaled(sx, sy), sx, sy, o);
                    Log.Write(string.Format("客户区 {0}x{1}  缩放 x{2:0.0000} y{3:0.0000}", cw, chh, sx, sy));
                    if (Math.Abs(sx - sy) > 0.004)
                        Log.Write("注意：客户区不是 16:9，识别位置会被拉伸");
                }

                var org = new Win32.POINT { X = 0, Y = 0 };
                if (!Win32.ClientToScreen(hwnd, ref org)) { hwnd = IntPtr.Zero; continue; }
                int ox = org.X, oy = org.Y;

                var wr = stats.WidgetRect;
                if (wr.Width > 0 && det.Overlaps(ox, oy, wr, out var what))
                {
                    Status("浮窗挡住「" + what + "」");
                    Thread.Sleep(500);
                    continue;
                }

                var now = DateTime.Now;
                if (engine.State == 2)
                {
                    if (det.DetectEnd(ox, oy, out _)) engine.OnEnd(now);
                }
                else if (engine.State == 1)
                {
                    engine.OnTurn(det.DetectTurn(ox, oy, out _, out _), now);
                    if (engine.State == 1)
                        engine.OnCoin(det.DetectCoin(ox, oy, out _, out _, out _, out _), now);
                }
                else
                {
                    engine.OnCoin(det.DetectCoin(ox, oy, out _, out _, out _, out _), now);
                }
                engine.Tick(now);

                Status(engine.State == 2 ? "对局中" : "已锁定 " + cw + "x" + chh);
                Thread.Sleep(poll);
            }
        }
        catch (Exception e)
        {
            Log.Write("识别线程崩了: " + e);
            stats.SetStatus("出错了，看日志");
        }
    }
}
