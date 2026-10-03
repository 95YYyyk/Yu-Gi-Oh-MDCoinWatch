using System.Reflection;
using System.Runtime.InteropServices;

namespace MdCoinWatch;

/// <summary>一枚硬币的位图：预乘 alpha 的 BGRA，按需要缩放到显示尺寸，用 AlphaBlend 贴上去。</summary>
internal sealed unsafe class CoinBitmap : IDisposable
{
    private readonly byte[] _src;
    private readonly int _sw, _sh;
    private nint _dc, _bmp, _old;
    internal int W { get; private set; }
    internal int H { get; private set; }

    internal CoinBitmap(byte[] src, int sw, int sh)
    {
        _src = src; _sw = sw; _sh = sh;
        _dc = Ui32.CreateCompatibleDC(nint.Zero);
    }

    internal void Resize(int w, int h)
    {
        if (w < 2 || h < 2) return;
        if (w == W && h == H && _bmp != nint.Zero) return;

        if (_bmp != nint.Zero)
        {
            Ui32.SelectObject(_dc, _old);
            Ui32.DeleteObject(_bmp);
            _bmp = nint.Zero;
        }

        var bmi = new Win32.BITMAPINFO();
        bmi.bmiHeader.biSize = Marshal.SizeOf<Win32.BITMAPINFOHEADER>();
        bmi.bmiHeader.biWidth = w;
        bmi.bmiHeader.biHeight = -h;
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        _bmp = Ui32.CreateDIBSection(_dc, ref bmi, 0, out nint bits, nint.Zero, 0);
        if (_bmp == nint.Zero) return;

        var dst = new Span<byte>((void*)bits, w * h * 4);
        for (int y = 0; y < h; y++)
        {
            double sy = (y + 0.5) * _sh / h - 0.5;
            int y0 = (int)Math.Floor(sy);
            double fy = sy - y0;
            int y1 = Math.Clamp(y0 + 1, 0, _sh - 1);
            y0 = Math.Clamp(y0, 0, _sh - 1);
            for (int x = 0; x < w; x++)
            {
                double sx = (x + 0.5) * _sw / w - 0.5;
                int x0 = (int)Math.Floor(sx);
                double fx = sx - x0;
                int x1 = Math.Clamp(x0 + 1, 0, _sw - 1);
                x0 = Math.Clamp(x0, 0, _sw - 1);
                int a = (y0 * _sw + x0) * 4, b = (y0 * _sw + x1) * 4;
                int c = (y1 * _sw + x0) * 4, d = (y1 * _sw + x1) * 4;
                int o = (y * w + x) * 4;
                for (int k = 0; k < 4; k++)
                {
                    double top = _src[a + k] + (_src[b + k] - _src[a + k]) * fx;
                    double bot = _src[c + k] + (_src[d + k] - _src[c + k]) * fx;
                    dst[o + k] = (byte)Math.Clamp(top + (bot - top) * fy, 0.0, 255.0);
                }
            }
        }

        _old = Ui32.SelectObject(_dc, _bmp);
        W = w; H = h;
    }

    internal void Draw(nint hdc, int x, int y)
    {
        if (_bmp == nint.Zero) return;
        var bf = new Ui32.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
        Ui32.AlphaBlend(hdc, x, y, W, H, _dc, 0, 0, W, H, bf);
    }

    public void Dispose()
    {
        if (_bmp != nint.Zero) { Ui32.SelectObject(_dc, _old); Ui32.DeleteObject(_bmp); _bmp = nint.Zero; }
        if (_dc != nint.Zero) { Ui32.DeleteDC(_dc); _dc = nint.Zero; }
    }
}

/// <summary>
/// 无边框悬浮窗，纯 Win32 + GDI 自绘，不依赖 WinForms —— 这样自带运行时版才能被裁剪到十几 MB。
/// WS_EX_NOACTIVATE：点它、拖它、缩放它都不会抢走游戏的前台焦点。
/// </summary>
internal sealed class Widget : IDisposable
{
    private const double BaseWidth = 248;
    private const double BaseHeight = 88;
    private const double BodyPt = 10.5;
    private const double SmallPt = 8.5;
    private const int FontWeight = 700;
    private const string ClassName = "YuGiOhMDCoinWatchWidget";
    private const int MF_POPUP = 0x00000010;

    private static Ui32.WndProc? _keepAlive;
    private static bool _classReady;

    private readonly Stats _stats;
    private readonly Settings _cfg;
    private readonly Recorder _rec;
    private nint _hwnd;
    private nint _fontBody, _fontSmall;
    private nint _brPanel, _brBorder, _brDot, _penGrip;
    private nint _curArrow, _curWE, _curNS, _curNWSE, _curNESW, _curAll;
    private CoinBitmap? _coinFront, _coinBack;

    private int _w, _h, _dpi = 96;
    private int _hBody, _hSmall;
    private double _fontScale = -1;

    private Snapshot _snap = new();
    private int _colorText = 0xEEF2F8, _colorAccent = 0xFFC53D, _colorDim = 0x7C8496;
    private bool _showInTaskbar;
    private nint _icon;
    private bool _locked;
    private bool _moving, _resizing;
    private int _zone;
    private int _dragX, _dragY;
    private Win32.RECT _dragRect;
    private int _hoverZone;

    internal nint Handle => _hwnd;

    internal Widget(Stats stats, Settings cfg, Recorder rec)
    {
        _stats = stats;
        _cfg = cfg;
        _rec = rec;
        _snap = stats.Read();

        _dpi = 96;
        _showInTaskbar = cfg.ShowInTaskbar;
        _w = Dpi(Math.Clamp(cfg.Width, 200, 720));
        _h = (int)Math.Round(_w * BaseHeight / BaseWidth);

        var wa = Screen_WorkingArea();
        int x = cfg.X == int.MinValue ? Math.Max(wa.Left, wa.Right - _w - Dpi(24)) : cfg.X;
        int y = cfg.Y == int.MinValue ? wa.Top + Dpi(24) : cfg.Y;

        EnsureClass();
        _hwnd = Ui32.CreateWindowExW(
            ExStyle(), ClassName, "Yu-Gi-Oh MDCoinWatch", Ui32.WS_POPUP, x, y, _w, _h,
            nint.Zero, nint.Zero, nint.Zero, nint.Zero);
        if (_hwnd == nint.Zero) throw new InvalidOperationException("创建窗口失败");

        uint d = Ui32.GetDpiForWindow(_hwnd);
        if (d > 0) _dpi = (int)d;
        _w = Dpi(Math.Clamp(cfg.Width, 200, 720));
        _h = (int)Math.Round(_w * BaseHeight / BaseWidth);
        Ui32.SetWindowPos(_hwnd, new nint(Ui32.HWND_TOPMOST), x, y, _w, _h, Ui32.SWP_NOACTIVATE);

        MakeBrushes();
        MakeFonts();
        LoadCoins();
        LoadIcon();
        ApplyShape();
        ApplyOpacity();

        _curArrow = Ui32.LoadCursorW(nint.Zero, new nint(Ui32.IDC_ARROW));
        _curWE = Ui32.LoadCursorW(nint.Zero, new nint(Ui32.IDC_SIZEWE));
        _curNS = Ui32.LoadCursorW(nint.Zero, new nint(Ui32.IDC_SIZENS));
        _curNWSE = Ui32.LoadCursorW(nint.Zero, new nint(Ui32.IDC_SIZENWSE));
        _curNESW = Ui32.LoadCursorW(nint.Zero, new nint(Ui32.IDC_SIZENESW));
        _curAll = Ui32.LoadCursorW(nint.Zero, new nint(Ui32.IDC_SIZEALL));

        Ui32.SetTimer(_hwnd, 1, 500, nint.Zero);
        Ui32.ShowWindow(_hwnd, Ui32.SW_SHOWNOACTIVATE);
        Publish();

        Log.Write("浮窗 " + _w + "x" + _h + " DPI=" + _dpi + " (原生自绘)");
    }

    private int Dpi(double v) => (int)Math.Round(v * _dpi / 96.0);
    private double UiScale => _w / (double)Dpi(BaseWidth);
    private int P(double v) => (int)Math.Round(v * UiScale * _dpi / 96.0);

    private static Win32.RECT Screen_WorkingArea()
    {
        // 主屏工作区：用 SystemParametersInfo 拿，避开 WinForms
        var r = new Win32.RECT { Left = 0, Top = 0, Right = Ui32.GetSystemMetrics(0), Bottom = Ui32.GetSystemMetrics(1) };
        try
        {
            var rc = new Win32.RECT();
            if (Ui32.SystemParametersInfoW(0x0030 /*SPI_GETWORKAREA*/, 0, ref rc, 0)) return rc;
        }
        catch { }
        return r;
    }

    private void EnsureClass()
    {
        if (_classReady) return;
        _keepAlive = WndProcThunk;
        var wc = new Ui32.WNDCLASSEXW
        {
            cbSize = Marshal.SizeOf<Ui32.WNDCLASSEXW>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_keepAlive),
            hInstance = nint.Zero,
            lpszClassName = ClassName
        };
        Ui32.RegisterClassExW(ref wc);
        _classReady = true;
    }

    private void MakeBrushes()
    {
        var (pr, pg, pb) = Rgb(_cfg.PanelColor, 14, 16, 21);
        _brPanel = Ui32.CreateSolidBrush(Ui32.Rgb(pr, pg, pb));
        _brBorder = Ui32.CreateSolidBrush(Ui32.Rgb(Blend(pr, 255, 0.20), Blend(pg, 255, 0.20), Blend(pb, 255, 0.20)));
        _brDot = Ui32.CreateSolidBrush(Ui32.Rgb(110, 118, 134));
        _penGrip = Ui32.CreatePen(0, Math.Max(1, P(1)), Ui32.Rgb(Blend(pr, 255, 0.28), Blend(pg, 255, 0.28), Blend(pb, 255, 0.28)));

        var (tr, tg, tb) = Rgb(_cfg.TextColor, 238, 242, 248);
        var (ar, ag, ab) = Rgb(_cfg.AccentColor, 255, 197, 61);
        var (dr, dg, db) = Rgb(_cfg.DimColor, 124, 132, 150);
        _colorText = (int)Ui32.Rgb(tr, tg, tb);
        _colorAccent = (int)Ui32.Rgb(ar, ag, ab);
        _colorDim = (int)Ui32.Rgb(dr, dg, db);
    }

    private static byte Blend(byte a, byte b, double t) => (byte)Math.Clamp(a + (b - a) * t, 0, 255);

    private void RemakeColors()
    {
        if (_brBorder != nint.Zero) Ui32.DeleteObject(_brBorder);
        if (_penGrip != nint.Zero) Ui32.DeleteObject(_penGrip);
        var (pr, pg, pb) = Rgb(_cfg.PanelColor, 14, 16, 21);
        _brBorder = Ui32.CreateSolidBrush(Ui32.Rgb(Blend(pr, 255, 0.20), Blend(pg, 255, 0.20), Blend(pb, 255, 0.20)));
        _penGrip = Ui32.CreatePen(0, Math.Max(1, P(1)), Ui32.Rgb(Blend(pr, 255, 0.28), Blend(pg, 255, 0.28), Blend(pb, 255, 0.28)));
        var (tr, tg, tb) = Rgb(_cfg.TextColor, 238, 242, 248);
        var (ar, ag, ab) = Rgb(_cfg.AccentColor, 255, 197, 61);
        var (dr, dg, db) = Rgb(_cfg.DimColor, 124, 132, 150);
        _colorText = (int)Ui32.Rgb(tr, tg, tb);
        _colorAccent = (int)Ui32.Rgb(ar, ag, ab);
        _colorDim = (int)Ui32.Rgb(dr, dg, db);
    }

    private void MakeFonts()
    {
        double sc = UiScale;
        if (_fontBody != nint.Zero && Math.Abs(sc - _fontScale) < 0.01) return;
        _fontScale = sc;
        if (_fontBody != nint.Zero) Ui32.DeleteObject(_fontBody);
        if (_fontSmall != nint.Zero) Ui32.DeleteObject(_fontSmall);
        _fontBody = MakeFont(BodyPt * sc);
        _fontSmall = MakeFont(SmallPt * sc);

        var hdc = Ui32.GetDC(_hwnd);
        _hBody = MeasureLine(hdc, _fontBody);
        _hSmall = MeasureLine(hdc, _fontSmall);
        Ui32.ReleaseDC(_hwnd, hdc);
    }

    private nint MakeFont(double pt)
    {
        int h = -(int)Math.Round(pt * _dpi / 72.0);
        return Ui32.CreateFontW(h, 0, 0, 0, FontWeight, 0, 0, 0, 1, 0, 0, 5, 0, "Microsoft YaHei UI");
    }

    private void LoadCoins()
    {
        var f = LoadBytes("coin_front.bgra");
        var b = LoadBytes("coin_back.bgra");
        if (f != null) _coinFront = new CoinBitmap(f, 128, 128);
        if (b != null) _coinBack = new CoinBitmap(b, 128, 128);
    }

    private static byte[]? LoadBytes(string name)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var res = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
            if (res == null) { Log.Write("缺少内嵌资源 " + name); return null; }
            using var s = asm.GetManifestResourceStream(res)!;
            var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
        catch (Exception e) { Log.Write("读资源失败 " + name + ": " + e.Message); return null; }
    }

    private void ApplyShape()
    {
        int r = Math.Max(6, P(12));
        var rgn = Ui32.CreateRoundRectRgn(0, 0, _w + 1, _h + 1, r * 2, r * 2);
        Ui32.SetWindowRgn(_hwnd, rgn, true);
    }

    private void ApplyOpacity()
    {
        byte a = (byte)Math.Clamp(_cfg.Opacity * 255 / 100, 20, 255);
        Ui32.SetLayeredWindowAttributes(_hwnd, 0, a, Ui32.LWA_ALPHA);
    }

    /// <summary>
    /// 按当前设置拼扩展样式。OBS 的窗口列表会跳过带 WS_EX_TOOLWINDOW 的窗口
    /// （见 libobs/util/windows/window-helpers.c 的 check_window_valid），
    /// 所以「显示在任务栏」同时决定了 OBS 能不能在列表里看到这个浮窗。
    /// </summary>
    private int ExStyle()
    {
        int ex = Ui32.WS_EX_TOPMOST | Ui32.WS_EX_LAYERED | Ui32.WS_EX_NOACTIVATE;
        if (!_showInTaskbar) ex |= Ui32.WS_EX_TOOLWINDOW;
        return ex;
    }

    private void ToggleTaskbar()
    {
        _showInTaskbar = !_showInTaskbar;
        _cfg.ShowInTaskbar = _showInTaskbar;
        _cfg.Save();

        Ui32.SetWindowLongPtr(_hwnd, Ui32.GWL_EXSTYLE, new nint(ExStyle()));
        // 任务栏按钮只在这对「隐藏 + 显示」之后才会重新评估
        Ui32.ShowWindow(_hwnd, Ui32.SW_HIDE);
        Ui32.ShowWindow(_hwnd, Ui32.SW_SHOWNOACTIVATE);
        ApplyOpacity();
        ApplyShape();
        Publish();
        Ui32.InvalidateRect(_hwnd, nint.Zero, false);
        Log.Write("任务栏显示: " + (_showInTaskbar ? "开" : "关"));
    }

    private void LoadIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            _icon = Ui32.ExtractIconW(nint.Zero, exe, 0);
            if (_icon == nint.Zero) return;
            Ui32.PostMessageW(_hwnd, Ui32.WM_SETICON, new nint(Ui32.ICON_BIG), _icon);
            Ui32.PostMessageW(_hwnd, Ui32.WM_SETICON, new nint(Ui32.ICON_SMALL), _icon);
        }
        catch (Exception e) { Log.Write("加载窗口图标失败: " + e.Message); }
    }

    /// <summary>清除全部对局记录。破坏性操作，先弹一个默认选「否」的二次确认。</summary>
    private void ClearRecords()
    {
        int r = Ui32.MessageBoxW(_hwnd,
            "确定要清除全部对局记录吗？\n\n会把 CSV 里所有记录（含以前几天）都删掉，只留表头。此操作无法撤销。",
            "清除所有记录", 0x4 | 0x30 | 0x100);          // MB_YESNO | MB_ICONWARNING | MB_DEFBUTTON2
        if (r != 6) return;                                // IDYES
        _rec.Reset();
        _stats.Clear();
        _snap = _stats.Read();
        Ui32.InvalidateRect(_hwnd, nint.Zero, false);
        Log.Write("已清除全部对局记录");
    }

    private void Publish()
    {
        int x = 0, y = 0, w = _w, h = _h;
        if (Ui32.GetWindowRect(_hwnd, out var r))
        {
            x = r.Left; y = r.Top; w = r.Right - r.Left; h = r.Bottom - r.Top;
        }
        _stats.SetWidgetRect(new System.Drawing.Rectangle(x, y, w, h));
    }

    // ---------- 消息处理 ----------
    private nint WndProcThunk(nint hwnd, int msg, nint wParam, nint lParam)
    {
        try { return OnMessage(hwnd, msg, wParam, lParam); }
        catch (Exception e) { Log.Write("窗口消息出错: " + e.Message); return Ui32.DefWindowProcW(hwnd, msg, wParam, lParam); }
    }

    private nint OnMessage(nint hwnd, int msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case Ui32.WM_PAINT: OnPaint(); return nint.Zero;
            case Ui32.WM_ERASEBKGND: return new nint(1);
            case Ui32.WM_TIMER:
                _snap = _stats.Read();
                Ui32.InvalidateRect(hwnd, nint.Zero, false);
                return nint.Zero;
            case Ui32.WM_LBUTTONDOWN:
                OnLeftDown(lParam);
                return nint.Zero;
            case Ui32.WM_MOUSEMOVE:
                OnMouseMove(lParam);
                return nint.Zero;
            case Ui32.WM_LBUTTONUP:
                OnLeftUp();
                return nint.Zero;
            case Ui32.WM_RBUTTONUP:
                ShowMenu();
                return nint.Zero;
            case Ui32.WM_SETCURSOR:
                Ui32.SetCursor(CursorFor(_moving || _resizing ? _zone : _hoverZone));
                return new nint(1);
            case Ui32.WM_CLOSE:
                Ui32.DestroyWindow(hwnd);
                return nint.Zero;
            case Ui32.WM_DESTROY:
                Ui32.KillTimer(hwnd, 1);
                Ui32.PostQuitMessage(0);
                return nint.Zero;
            default:
                return Ui32.DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }

    private static int LoWord(nint v) => (short)((long)v & 0xFFFF);
    private static int HiWord(nint v) => (short)(((long)v >> 16) & 0xFFFF);

    private void OnLeftDown(nint lParam)
    {
        int x = LoWord(lParam), y = HiWord(lParam);
        _dragX = Ui32.GetCursorPos(out var p) ? p.X : x;
        _dragY = p.Y;
        Ui32.GetWindowRect(_hwnd, out _dragRect);
        _zone = HitZone(x, y);
        _resizing = _zone != 0 && !_locked;
        _moving = !_resizing && !_locked;
        if (!_locked) Ui32.SetCapture(_hwnd);
    }

    private void OnMouseMove(nint lParam)
    {
        int x = LoWord(lParam), y = HiWord(lParam);
        if (_locked)
        {
            _hoverZone = -1;
            return;
        }
        if (!_moving && !_resizing) { _hoverZone = HitZone(x, y); Ui32.SetCursor(CursorFor(_hoverZone)); return; }

        Ui32.GetCursorPos(out var cur);
        if (_resizing) DoResize(cur.X, cur.Y);
        else
        {
            int nx = _dragRect.Left + cur.X - _dragX;
            int ny = _dragRect.Top + cur.Y - _dragY;
            Ui32.SetWindowPos(_hwnd, new nint(-1), nx, ny, 0, 0, Ui32.SWP_NOSIZE | Ui32.SWP_NOACTIVATE);
            Publish();
        }
    }

    private void OnLeftUp()
    {
        if (!_moving && !_resizing) return;
        _moving = _resizing = false;
        _zone = 0;
        Ui32.ReleaseCapture();
        SavePlacement();
    }

    private int HitZone(int x, int y)
    {
        int m = Math.Max(4, P(7));
        bool l = x <= m, r = x >= _w - m, t = y <= m, b = y >= _h - m;
        if (l && t) return 1;
        if (r && t) return 2;
        if (l && b) return 3;
        if (r && b) return 4;
        if (l) return 5;
        if (r) return 6;
        if (t) return 7;
        if (b) return 8;
        return 0;
    }

    private nint CursorFor(int zone) => zone switch
    {
        5 or 6 => _curWE,
        7 or 8 => _curNS,
        1 or 4 => _curNWSE,
        2 or 3 => _curNESW,
        _ => _curAll
    };

    private void DoResize(int curX, int curY)
    {
        int dx = curX - _dragX, dy = curY - _dragY;
        int z = _zone;
        double bw = _dragRect.Right - _dragRect.Left, bh = _dragRect.Bottom - _dragRect.Top;
        double delta;

        if (z == 7 || z == 8) delta = dy * bw / bh;                 // 上下边：竖直变化折成等效横向
        else delta = dx;
        if (z == 7 || z == 1 || z == 2) delta = -dy * bw / bh;      // 带上边：反向
        else if (z == 5 || z == 1 || z == 3) delta = -dx;           // 带左边：反向
        if (z == 1 || z == 2 || z == 3 || z == 4)
        {
            double d = (z == 1 || z == 2) ? -dy * bw / bh : dy * bw / bh;
            if (Math.Abs(d) > Math.Abs(delta)) delta = d;
        }

        double newW = Math.Clamp(bw + delta, Dpi(200), Dpi(720));
        double newH = newW * BaseHeight / BaseWidth;
        int left = _dragRect.Left, top = _dragRect.Top;
        if (z == 5 || z == 1 || z == 3) left = (int)Math.Round(_dragRect.Right - newW);
        if (z == 7 || z == 1 || z == 2) top = (int)Math.Round(_dragRect.Bottom - newH);

        _w = (int)Math.Round(newW);
        _h = (int)Math.Round(newH);
        Ui32.SetWindowPos(_hwnd, new nint(-1), left, top, _w, _h, Ui32.SWP_NOACTIVATE);
        ApplyShape();
        MakeFonts();
        Publish();
        Ui32.InvalidateRect(_hwnd, nint.Zero, false);
    }

    private void SavePlacement()
    {
        Ui32.GetWindowRect(_hwnd, out var r);
        _cfg.X = r.Left;
        _cfg.Y = r.Top;
        _cfg.Width = Math.Clamp((int)Math.Round(_w * 96.0 / _dpi), 200, 720);
        _cfg.Save();
    }

    // ---------- 绘制 ----------
    private void OnPaint()
    {
        var ps = new Ui32.PAINTSTRUCT();
        var hdc = Ui32.BeginPaint(_hwnd, out ps);
        var memDc = Ui32.CreateCompatibleDC(hdc);
        var bmp = Ui32.CreateCompatibleBitmap(hdc, _w, _h);
        var old = Ui32.SelectObject(memDc, bmp);

        Draw(memDc);

        Ui32.BitBlt(hdc, 0, 0, _w, _h, memDc, 0, 0, Ui32.SRCCOPY);
        Ui32.SelectObject(memDc, old);
        Ui32.DeleteObject(bmp);
        Ui32.DeleteDC(memDc);
        Ui32.EndPaint(_hwnd, ref ps);
    }

    private void Draw(nint hdc)
    {
        var s = _snap;
        var all = new Win32.RECT { Left = 0, Top = 0, Right = _w, Bottom = _h };
        Ui32.FillRect(hdc, ref all, _brPanel);
        Ui32.FrameRect(hdc, ref all, _brBorder);

        int pad = P(10), icon = P(25), gap = P(6), colX = P(122), row1 = P(8), row2 = P(40), row3 = P(66);
        int cy = row1 + icon / 2;

        _coinFront?.Resize(icon, icon);
        _coinBack?.Resize(icon, icon);

        int x = pad;
        _coinFront?.Draw(hdc, x, row1);
        int w1 = TextW(hdc, s.Front.ToString(), _fontBody);
        Text(hdc, s.Front.ToString(), x + icon + gap, cy - _hBody / 2, _fontBody, _colorDim);
        Text(hdc, Pct(s.Front, s.Total), x + icon + gap + w1 + gap, cy - _hBody / 2, _fontBody, _colorAccent);

        x = colX;
        _coinBack?.Draw(hdc, x, row1);
        int w2 = TextW(hdc, s.Back.ToString(), _fontBody);
        Text(hdc, s.Back.ToString(), x + icon + gap, cy - _hBody / 2, _fontBody, _colorDim);
        Text(hdc, Pct(s.Back, s.Total), x + icon + gap + w2 + gap, cy - _hBody / 2, _fontBody, _colorAccent);

        // 第二行：先/后 12/24   赢/输 4/9
        // 「/」左边一个颜色、右边一个颜色，前后两组用同一对颜色
        int cOn = _colorAccent, cOff = _colorText, cSlash = _colorDim;
        int sp = P(9);
        int x2 = pad;
        x2 = Seg(hdc, "先", x2, row2, _fontBody, cOn);
        x2 = Seg(hdc, "/", x2, row2, _fontBody, cSlash);
        x2 = Seg(hdc, "后", x2, row2, _fontBody, cOff);
        x2 += sp;
        x2 = Seg(hdc, s.First.ToString(), x2, row2, _fontBody, cOn);
        x2 = Seg(hdc, "/", x2, row2, _fontBody, cSlash);
        x2 = Seg(hdc, s.Second.ToString(), x2, row2, _fontBody, cOff);

        x2 = colX;
        x2 = Seg(hdc, "胜", x2, row2, _fontBody, cOn);
        x2 = Seg(hdc, "/", x2, row2, _fontBody, cSlash);
        x2 = Seg(hdc, "负", x2, row2, _fontBody, cOff);
        x2 += sp;
        x2 = Seg(hdc, s.Wins.ToString(), x2, row2, _fontBody, cOn);
        x2 = Seg(hdc, "/", x2, row2, _fontBody, cSlash);
        x2 = Seg(hdc, s.Losses.ToString(), x2, row2, _fontBody, cOff);

        int lw = TextW(hdc, "今日运势：", _fontSmall);
        Text(hdc, "今日运势：", pad, row3, _fontSmall, _colorDim);
        // 右边给缩放示意留一块，别让长文案压上去
        TextClip(hdc, s.Fortune, pad + lw, row3, _w - pad - lw - pad - P(8), _fontSmall, _colorAccent);

        // 状态点
        uint dot = s.Status.StartsWith("已锁定") ? Ui32.Rgb(90, 220, 130)
                 : s.Status.StartsWith("对局中") ? Ui32.Rgb(255, 197, 61)
                 : Ui32.Rgb(110, 118, 134);
        var br = Ui32.CreateSolidBrush(dot);
        var pn = Ui32.CreatePen(0, 1, dot);
        var o1 = Ui32.SelectObject(hdc, br);
        var o2 = Ui32.SelectObject(hdc, pn);
        int d = Math.Max(5, P(8));
        Ui32.Ellipse(hdc, _w - pad - d, P(14), _w - pad, P(14) + d);
        Ui32.SelectObject(hdc, o1);
        Ui32.SelectObject(hdc, o2);
        Ui32.DeleteObject(br);
        Ui32.DeleteObject(pn);

        // 右下角缩放示意
        var og = Ui32.SelectObject(hdc, _penGrip);
        int gx = _w - P(4), gy = _h - P(4), step = P(4);
        for (int i = 1; i <= 3; i++)
        {
            Ui32.MoveToEx(hdc, gx - i * step, gy, nint.Zero);
            Ui32.LineTo(hdc, gx, gy - i * step);
        }
        Ui32.SelectObject(hdc, og);
    }

    private static string Pct(int n, int total) => total == 0 ? "--" : Math.Round(100.0 * n / total) + "%";

    private int MeasureLine(nint hdc, nint font)
    {
        var old = Ui32.SelectObject(hdc, font);
        var r = new Win32.RECT { Left = 0, Top = 0, Right = 0, Bottom = 0 };
        Ui32.DrawTextW(hdc, "汉Hg", -1, ref r, Ui32.DT_CALCRECT | Ui32.DT_SINGLELINE | Ui32.DT_NOPREFIX);
        Ui32.SelectObject(hdc, old);
        return Math.Max(8, r.Bottom - r.Top);
    }

    private int TextW(nint hdc, string s, nint font)
    {
        var old = Ui32.SelectObject(hdc, font);
        var r = new Win32.RECT { Left = 0, Top = 0, Right = 0, Bottom = 0 };
        Ui32.DrawTextW(hdc, s, -1, ref r, Ui32.DT_CALCRECT | Ui32.DT_SINGLELINE | Ui32.DT_NOPREFIX | Ui32.DT_LEFT);
        Ui32.SelectObject(hdc, old);
        return r.Right - r.Left;
    }

    private void Text(nint hdc, string s, int x, int y, nint font, int color)
    {
        Ui32.SelectObject(hdc, font);
        Ui32.SetTextColor(hdc, (uint)color);
        Ui32.SetBkMode(hdc, Ui32.TRANSPARENT);
        var r = new Win32.RECT { Left = x, Top = y, Right = x + _w, Bottom = y + P(40) };
        Ui32.DrawTextW(hdc, s, -1, ref r, Ui32.DT_SINGLELINE | Ui32.DT_NOPREFIX | Ui32.DT_LEFT);
    }

    private void TextClip(nint hdc, string s, int x, int y, int maxW, nint font, int color)
    {
        Ui32.SelectObject(hdc, font);
        Ui32.SetTextColor(hdc, (uint)color);
        Ui32.SetBkMode(hdc, Ui32.TRANSPARENT);
        var r = new Win32.RECT { Left = x, Top = y, Right = x + Math.Max(P(30), maxW), Bottom = y + P(24) };
        Ui32.DrawTextW(hdc, s, -1, ref r, Ui32.DT_SINGLELINE | Ui32.DT_NOPREFIX | Ui32.DT_LEFT | Ui32.DT_END_ELLIPSIS);
    }

    /// <summary>画一段文字，返回画完之后的 x。第二行要逐段上色，所以得一段段排。</summary>
    private int Seg(nint hdc, string s, int x, int y, nint font, int color)
    {
        Text(hdc, s, x, y, font, color);
        return x + TextW(hdc, s, font);
    }

    // ---------- 右键菜单 ----------
    private void ShowMenu()
    {
        var snap = _stats.Read();
        var menu = Ui32.CreatePopupMenu();

        var c1 = Ui32.CreatePopupMenu();
        AddColor(c1, 1, "浅白");
        AddColor(c1, 2, "浅黄");
        AddColor(c1, 3, "浅青");
        AddColor(c1, 4, "浅粉");
        Ui32.AppendMenuW(c1, Ui32.MF_SEPARATOR, nint.Zero, null!);
        Ui32.AppendMenuW(c1, Ui32.MF_STRING, new nint(5), "自定义…");
        Ui32.AppendMenuW(menu, MF_POPUP, c1, "文字颜色");

        var c2 = Ui32.CreatePopupMenu();
        AddColor(c2, 11, "金色");
        AddColor(c2, 12, "青色");
        AddColor(c2, 13, "粉色");
        AddColor(c2, 14, "绿色");
        Ui32.AppendMenuW(c2, Ui32.MF_SEPARATOR, nint.Zero, null!);
        Ui32.AppendMenuW(c2, Ui32.MF_STRING, new nint(15), "自定义…");
        Ui32.AppendMenuW(menu, MF_POPUP, c2, "强调色");

        var c3 = Ui32.CreatePopupMenu();
        foreach (var (name, id) in new[] { ("100%", 21), ("92%", 22), ("80%", 23), ("65%", 24), ("50%", 25) })
            Ui32.AppendMenuW(c3, Ui32.MF_STRING, new nint(id), name);
        Ui32.AppendMenuW(menu, MF_POPUP, c3, "不透明度");

        Ui32.AppendMenuW(menu, Ui32.MF_SEPARATOR, nint.Zero, null!);
        Ui32.AppendMenuW(menu, Ui32.MF_GRAYED | Ui32.MF_DISABLED, nint.Zero, snap.AllRateText);

        Ui32.AppendMenuW(menu, Ui32.MF_SEPARATOR, nint.Zero, null!);
        Ui32.AppendMenuW(menu, Ui32.MF_STRING | (_showInTaskbar ? Ui32.MF_CHECKED : 0), new nint(41), "显示在任务栏");
        Ui32.AppendMenuW(menu, Ui32.MF_STRING | (_locked ? Ui32.MF_CHECKED : 0), new nint(40), "锁定位置与大小");
        Ui32.AppendMenuW(menu, Ui32.MF_STRING, new nint(50), "打开记录文件");
        Ui32.AppendMenuW(menu, Ui32.MF_STRING, new nint(51), "打开配置与日志");
        Ui32.AppendMenuW(menu, Ui32.MF_SEPARATOR, nint.Zero, null!);
        Ui32.AppendMenuW(menu, Ui32.MF_STRING, new nint(70), "清除所有记录数据…");
        Ui32.AppendMenuW(menu, Ui32.MF_SEPARATOR, nint.Zero, null!);
        Ui32.AppendMenuW(menu, Ui32.MF_STRING, new nint(60), "退出");

        Ui32.GetCursorPos(out var pt);
        Ui32.SetForegroundWindow(_hwnd);
        int cmd = Ui32.TrackPopupMenuEx(menu, Ui32.TPM_RIGHTBUTTON | Ui32.TPM_RETURNCMD, pt.X, pt.Y, _hwnd, nint.Zero);
        Ui32.PostMessageW(_hwnd, Ui32.WM_NULL, nint.Zero, nint.Zero);
        Ui32.DestroyMenu(menu);

        switch (cmd)
        {
            case 1: SetHex("EEF2F8", true); break;
            case 2: SetHex("F5E6A8", true); break;
            case 3: SetHex("A8E6E0", true); break;
            case 4: SetHex("F5C2D0", true); break;
            case 5: PickColor(true); break;
            case 11: SetHex("FFC53D", false); break;
            case 12: SetHex("5FD3C4", false); break;
            case 13: SetHex("F58BA8", false); break;
            case 14: SetHex("7FD87F", false); break;
            case 15: PickColor(false); break;
            case 21: SetOpacity(100); break;
            case 22: SetOpacity(92); break;
            case 23: SetOpacity(80); break;
            case 24: SetOpacity(65); break;
            case 25: SetOpacity(50); break;
            case 40: _locked = !_locked; break;
            case 41: ToggleTaskbar(); break;
            case 50: OpenPath(_cfg.CsvPath); break;
            case 51: OpenPath(AppContext.BaseDirectory); break;
            case 70: ClearRecords(); break;
            case 60: Ui32.PostMessageW(_hwnd, Ui32.WM_CLOSE, nint.Zero, nint.Zero); break;
        }
    }

    private static void AddColor(nint menu, int id, string label) => Ui32.AppendMenuW(menu, Ui32.MF_STRING, new nint(id), label);

    private void SetHex(string hex, bool isText)
    {
        if (isText) _cfg.TextColor = "#" + hex; else _cfg.AccentColor = "#" + hex;
        _cfg.Save();
        RemakeColors();
        Ui32.InvalidateRect(_hwnd, nint.Zero, false);
    }

    private void SetOpacity(int v)
    {
        _cfg.Opacity = v;
        _cfg.Save();
        ApplyOpacity();
    }

    private void PickColor(bool isText)
    {
        var cust = Marshal.AllocHGlobal(16 * 4);
        try
        {
            var (r, g, b) = Rgb(isText ? _cfg.TextColor : _cfg.AccentColor, 255, 255, 255);
            var cc = new Ui32.CHOOSECOLORW
            {
                lStructSize = Marshal.SizeOf<Ui32.CHOOSECOLORW>(),
                hwndOwner = _hwnd,
                rgbResult = (int)Ui32.Rgb(r, g, b),
                lpCustColors = cust,
                Flags = 0x00000001 | 0x00000100
            };
            if (!Ui32.ChooseColorW(ref cc)) return;
            int c = cc.rgbResult;
            var hex = string.Format("{0:X2}{1:X2}{2:X2}", c & 0xFF, (c >> 8) & 0xFF, (c >> 16) & 0xFF);
            SetHex(hex, isText);
        }
        finally { Marshal.FreeHGlobal(cust); }
    }

    private static void OpenPath(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception e) { Log.Write("打开失败 " + path + ": " + e.Message); }
    }

    private static (byte r, byte g, byte b) Rgb(string hex, byte fr, byte fg, byte fb)
    {
        try
        {
            var h = hex.TrimStart('#');
            if (h.Length != 6) return (fr, fg, fb);
            return (Convert.ToByte(h.Substring(0, 2), 16), Convert.ToByte(h.Substring(2, 2), 16), Convert.ToByte(h.Substring(4, 2), 16));
        }
        catch { return (fr, fg, fb); }
    }

    public void Dispose()
    {
        if (_hwnd != nint.Zero) { Ui32.DestroyWindow(_hwnd); _hwnd = nint.Zero; }
        _coinFront?.Dispose();
        _coinBack?.Dispose();
        if (_icon != nint.Zero) { Ui32.DestroyIcon(_icon); _icon = nint.Zero; }
        foreach (var o in new[] { _fontBody, _fontSmall, _brPanel, _brBorder, _penGrip })
            if (o != nint.Zero) Ui32.DeleteObject(o);
    }
}
