using System.Runtime.InteropServices;

namespace MdCoinWatch;

/// <summary>
/// 「关于」窗口：和浮窗同一套视觉的小面板，纯 Win32 自绘。
/// GitHub / B 站两行可以点开，ESC 或右下角按钮关闭。
/// </summary>
internal sealed class About : IDisposable
{
    private const string ClassName = "YuGiOhMDCoinWatchAbout";
    private const int BaseW = 430, BaseH = 312;

    private static Ui32.WndProc? _keepAlive;
    private static bool _classReady;
    private static About? _open;

    private const string RepoText = "github.com/95YYyyk/Yu-Gi-Oh-MDCoinWatch";
    private const string RepoUrl = "https://github.com/95YYyyk/Yu-Gi-Oh-MDCoinWatch";
    private const string BiliText = "space.bilibili.com/20912478";
    private const string BiliUrl = "https://space.bilibili.com/20912478";
    private const string QuarkText = "pan.quark.cn/s/1241b8eabd4a";
    private const string QuarkUrl = "https://pan.quark.cn/s/1241b8eabd4a";

    private readonly Settings _cfg;
    private nint _hwnd;
    private int _w, _h, _dpi = 96;
    private int _hBody, _hSmall;
    private nint _fontTitle, _fontBody, _fontSmall;
    private nint _brPanel, _brBorder, _penLine, _curHand, _curArrow;
    private int _colorText, _colorDim, _colorAccent;
    private Win32.RECT _rGit, _rQuark, _rBili, _rClose;
    private int _hover = -1;

    private About(Settings cfg) { _cfg = cfg; }

    internal static void Show(nint owner, Settings cfg)
    {
        if (_open != null) { _open.Focus(); return; }
        var a = new About(cfg);
        try { a.Create(owner); }
        catch (Exception e) { Log.Write("关于窗口创建失败: " + e.Message); a.Dispose(); }
    }

    private void Focus()
    {
        if (_hwnd == nint.Zero) return;
        Ui32.SetForegroundWindow(_hwnd);
        Ui32.InvalidateRect(_hwnd, nint.Zero, false);
    }

    private int P(double v) => (int)Math.Round(v * _dpi / 96.0);

    private void Create(nint owner)
    {
        EnsureClass();
        _w = BaseW; _h = BaseH;

        _hwnd = Ui32.CreateWindowExW(Ui32.WS_EX_TOPMOST, ClassName, "关于", Ui32.WS_POPUP,
            0, 0, _w, _h, nint.Zero, nint.Zero, nint.Zero, nint.Zero);
        if (_hwnd == nint.Zero) throw new InvalidOperationException("创建窗口失败");

        uint d = Ui32.GetDpiForWindow(_hwnd);
        if (d > 0) _dpi = (int)d;
        _w = P(BaseW); _h = P(BaseH);

        Ui32.GetWindowRect(owner, out var or);
        int ox = or.Left, oy = or.Top, ow = or.Right - or.Left, oh = or.Bottom - or.Top;
        int x = ow > 0 ? ox + (ow - _w) / 2 : 200;
        int y = oh > 0 ? oy + (oh - _h) / 2 : 200;
        Ui32.SetWindowPos(_hwnd, new nint(Ui32.HWND_TOPMOST), x, y, _w, _h, 0);

        MakeResources();
        ApplyShape();
        ComputeRects();
        _open = this;
        Ui32.ShowWindow(_hwnd, Ui32.SW_SHOW);
        Ui32.SetForegroundWindow(_hwnd);
        Ui32.InvalidateRect(_hwnd, nint.Zero, false);
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

    private nint WndProcThunk(nint hwnd, int msg, nint wParam, nint lParam)
    {
        try { return OnMessage(hwnd, msg, wParam, lParam); }
        catch (Exception e) { Log.Write("关于窗口消息出错: " + e.Message); return Ui32.DefWindowProcW(hwnd, msg, wParam, lParam); }
    }

    private nint OnMessage(nint hwnd, int msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case Ui32.WM_PAINT: OnPaint(); return nint.Zero;
            case Ui32.WM_ERASEBKGND: return new nint(1);
            case Ui32.WM_LBUTTONDOWN:
                OnClick(LoWord(lParam), HiWord(lParam));
                return nint.Zero;
            case Ui32.WM_MOUSEMOVE:
                OnHover(LoWord(lParam), HiWord(lParam));
                return nint.Zero;
            case Ui32.WM_SETCURSOR:
                Ui32.SetCursor(_hover >= 0 ? _curHand : _curArrow);
                return new nint(1);
            case Ui32.WM_KEYDOWN:
                if ((long)wParam == Ui32.VK_ESCAPE) Close();
                return nint.Zero;
            case Ui32.WM_CLOSE:
                Close();
                return nint.Zero;
            case Ui32.WM_DESTROY:
                _open = null;
                return nint.Zero;
            default:
                return Ui32.DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }

    private static int LoWord(nint v) => (short)((long)v & 0xFFFF);
    private static int HiWord(nint v) => (short)(((long)v >> 16) & 0xFFFF);

    private static bool In(Win32.RECT r, int x, int y)
        => x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom;

    private void OnClick(int x, int y)
    {
        if (In(_rGit, x, y)) { Widget.OpenPath(RepoUrl); return; }
        if (In(_rQuark, x, y)) { Widget.OpenPath(QuarkUrl); return; }
        if (In(_rBili, x, y)) { Widget.OpenPath(BiliUrl); return; }
        if (In(_rClose, x, y)) { Close(); return; }
    }

    private void OnHover(int x, int y)
    {
        int h = In(_rGit, x, y) ? 0
              : In(_rQuark, x, y) ? 1
              : In(_rBili, x, y) ? 2
              : In(_rClose, x, y) ? 3 : -1;
        if (h == _hover) return;
        _hover = h;
        Ui32.SetCursor(h >= 0 ? _curHand : _curArrow);
        Ui32.InvalidateRect(_hwnd, nint.Zero, false);
    }

    private void Close()
    {
        if (_hwnd != nint.Zero) { Ui32.DestroyWindow(_hwnd); _hwnd = nint.Zero; }
        Dispose();
        _open = null;
    }

    private void MakeResources()
    {
        var (pr, pg, pb) = Rgb(_cfg.PanelColor, 14, 16, 21);
        _brPanel = Ui32.CreateSolidBrush(Ui32.Rgb(pr, pg, pb));
        _brBorder = Ui32.CreateSolidBrush(Ui32.Rgb(Blend(pr, 255, 0.20), Blend(pg, 255, 0.20), Blend(pb, 255, 0.20)));
        _penLine = Ui32.CreatePen(0, Math.Max(1, P(1)), Ui32.Rgb(Blend(pr, 255, 0.18), Blend(pg, 255, 0.18), Blend(pb, 255, 0.18)));

        var (tr, tg, tb) = Rgb(_cfg.TextColor, 238, 242, 248);
        var (dr, dg, db) = Rgb(_cfg.DimColor, 124, 132, 150);
        var (ar, ag, ab) = Rgb(_cfg.AccentColor, 255, 197, 61);
        _colorText = (int)Ui32.Rgb(tr, tg, tb);
        _colorDim = (int)Ui32.Rgb(dr, dg, db);
        _colorAccent = (int)Ui32.Rgb(ar, ag, ab);

        _fontTitle = MakeFont(13.0, 700);
        _fontBody = MakeFont(10.5, 400);
        _fontSmall = MakeFont(8.5, 400);

        var hdc = Ui32.GetDC(_hwnd);
        _hBody = MeasureLine(hdc, _fontBody);
        _hSmall = MeasureLine(hdc, _fontSmall);
        Ui32.ReleaseDC(_hwnd, hdc);

        _curArrow = Ui32.LoadCursorW(nint.Zero, new nint(Ui32.IDC_ARROW));
        _curHand = Ui32.LoadCursorW(nint.Zero, new nint(Ui32.IDC_HAND));
    }

    private nint MakeFont(double pt, int weight)
    {
        int h = -(int)Math.Round(pt * _dpi / 72.0);
        return Ui32.CreateFontW(h, 0, 0, 0, weight, 0, 0, 0, 1, 0, 0, 5, 0, "Microsoft YaHei UI");
    }

    private void ComputeRects()
    {
        var hdc = Ui32.GetDC(_hwnd);
        int pad = P(18);
        int yGit = P(96), yQuark = P(148), yBili = P(200);
        int wGit = TextW(hdc, RepoText, _fontBody);
        int wQuark = TextW(hdc, QuarkText, _fontBody);
        int wBili = TextW(hdc, BiliText, _fontBody);
        _rGit = new Win32.RECT { Left = pad, Top = yGit - P(3), Right = pad + wGit, Bottom = yGit + _hBody };
        _rQuark = new Win32.RECT { Left = pad, Top = yQuark - P(3), Right = pad + wQuark, Bottom = yQuark + _hBody };
        _rBili = new Win32.RECT { Left = pad, Top = yBili - P(3), Right = pad + wBili, Bottom = yBili + _hBody };
        _rClose = new Win32.RECT { Left = _w - P(104), Top = _h - P(46), Right = _w - P(18), Bottom = _h - P(16) };
        Ui32.ReleaseDC(_hwnd, hdc);
    }

    private void ApplyShape()
    {
        int r = Math.Max(6, P(12));
        var rgn = Ui32.CreateRoundRectRgn(0, 0, _w + 1, _h + 1, r * 2, r * 2);
        Ui32.SetWindowRgn(_hwnd, rgn, true);
    }

    private void OnPaint()
    {
        var ps = new Ui32.PAINTSTRUCT();
        var hdc = Ui32.BeginPaint(_hwnd, out ps);
        var mem = Ui32.CreateCompatibleDC(hdc);
        var bmp = Ui32.CreateCompatibleBitmap(hdc, _w, _h);
        var old = Ui32.SelectObject(mem, bmp);

        var all = new Win32.RECT { Left = 0, Top = 0, Right = _w, Bottom = _h };
        Ui32.FillRect(mem, ref all, _brPanel);
        Ui32.FrameRect(mem, ref all, _brBorder);

        int pad = P(18);
        Text(mem, "Yu-Gi-Oh MDCoinWatch", pad, P(16), _fontTitle, _colorAccent);
        Text(mem, "大师决斗投币运势浮窗 · v" + Program.Version, pad, P(46), _fontSmall, _colorDim);

        var op = Ui32.SelectObject(mem, _penLine);
        Ui32.MoveToEx(mem, pad, P(70), nint.Zero);
        Ui32.LineTo(mem, _w - pad, P(70));
        Ui32.SelectObject(mem, op);

        Text(mem, "GitHub · 源码与发布", pad, P(80), _fontSmall, _colorDim);
        Text(mem, RepoText, pad, P(96), _fontBody, _colorAccent);
        Underline(mem, pad, P(96) + _hBody - P(3), pad + TextW(mem, RepoText, _fontBody));

        Text(mem, "夸克网盘 · 更新下载", pad, P(132), _fontSmall, _colorDim);
        Text(mem, QuarkText, pad, P(148), _fontBody, _colorAccent);
        Underline(mem, pad, P(148) + _hBody - P(3), pad + TextW(mem, QuarkText, _fontBody));

        Text(mem, "哔哩哔哩 · 作者主页", pad, P(184), _fontSmall, _colorDim);
        Text(mem, BiliText, pad, P(200), _fontBody, _colorAccent);
        Underline(mem, pad, P(200) + _hBody - P(3), pad + TextW(mem, BiliText, _fontBody));

        Text(mem, "点链接用浏览器打开 · ESC 关闭", pad, P(242), _fontSmall, _colorDim);

        var b = _rClose;
        if (_hover == 3)
        {
            var hb = Ui32.CreateSolidBrush(Ui32.Rgb(38, 41, 50));
            Ui32.FillRect(mem, ref b, hb);
            Ui32.DeleteObject(hb);
        }
        Ui32.FrameRect(mem, ref b, _brBorder);
        int tw = TextW(mem, "关闭", _fontBody);
        Text(mem, "关闭", _rClose.Left + ((_rClose.Right - _rClose.Left) - tw) / 2,
             _rClose.Top + ((_rClose.Bottom - _rClose.Top) - _hBody) / 2, _fontBody, _colorText);

        Ui32.BitBlt(hdc, 0, 0, _w, _h, mem, 0, 0, Ui32.SRCCOPY);
        Ui32.SelectObject(mem, old);
        Ui32.DeleteObject(bmp);
        Ui32.DeleteDC(mem);
        Ui32.EndPaint(_hwnd, ref ps);
    }

    private void Underline(nint hdc, int x1, int y, int x2)
    {
        var op = Ui32.SelectObject(hdc, _penLine);
        Ui32.MoveToEx(hdc, x1, y, nint.Zero);
        Ui32.LineTo(hdc, x2, y);
        Ui32.SelectObject(hdc, op);
    }

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

    private static byte Blend(byte a, byte b, double t) => (byte)Math.Clamp(a + (b - a) * t, 0, 255);

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
        foreach (var o in new[] { _fontTitle, _fontBody, _fontSmall, _brPanel, _brBorder, _penLine })
            if (o != nint.Zero) Ui32.DeleteObject(o);
        _fontTitle = _fontBody = _fontSmall = nint.Zero;
        _brPanel = _brBorder = _penLine = nint.Zero;
    }
}

