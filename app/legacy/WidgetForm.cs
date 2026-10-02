using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Windows.Forms;

namespace MdCoinWatch;

/// <summary>
/// 无边框悬浮窗。带 WS_EX_NOACTIVATE，点它不会抢焦点，游戏始终是前台窗口，
/// 所以去点浮窗、拖它、缩放它都不会让识别暂停。
/// 中间拖动 = 移动，边缘拖动 = 等比缩放。
/// </summary>
internal sealed class WidgetForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    // 布局基准尺寸，实际绘制时按 ClientSize.Width 等比放大
    private const double BaseWidth = 300;
    private const double BaseHeight = 124;

    private enum Zone { None, L, R, T, B, TL, TR, BL, BR }

    private readonly Stats _stats;
    private readonly Settings _cfg;
    private readonly System.Windows.Forms.Timer _tick;
    private readonly Image? _coinFront;
    private readonly Image? _coinBack;

    private Font _fontBody = null!;
    private Font _fontSmall = null!;
    private double _fontScale;

    private Snapshot _snap = new();
    private bool _locked;
    private bool _moving;
    private bool _resizing;
    private Zone _zone = Zone.None;
    private Point _dragCursor;
    private Rectangle _dragRect;

    internal WidgetForm(Stats stats, Settings cfg)
    {
        _stats = stats;
        _cfg = cfg;

        _coinFront = LoadAsset("coin_front64.png");
        _coinBack = LoadAsset("coin_back64.png");

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        Text = "MdCoinWatch";
        BackColor = ParseColor(_cfg.PanelColor, Color.FromArgb(14, 16, 21));
        Opacity = Math.Clamp(_cfg.Opacity, 25, 100) / 100.0;

        int w = Math.Clamp(S(_cfg.Width), S(240), S(720));
        ClientSize = new Size(w, (int)Math.Round(w * BaseHeight / BaseWidth));

        var wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(
            _cfg.X == int.MinValue ? Math.Max(wa.Left, wa.Right - Width - S(24)) : _cfg.X,
            _cfg.Y == int.MinValue ? wa.Top + S(24) : _cfg.Y);

        BuildMenu();

        _snap = _stats.Read();
        _tick = new System.Windows.Forms.Timer { Interval = 500 };
        _tick.Tick += (_, _) => { _snap = _stats.Read(); Invalidate(); };
        _tick.Start();

        Log.Write("浮窗 " + ClientSize.Width + "x" + ClientSize.Height + " DPI=" + DeviceDpi);
    }

    private int S(double v) => (int)Math.Round(v * DeviceDpi / 96.0);
    private double UiScale => ClientSize.Width / (double)S(BaseWidth);
    private int P(double v) => (int)Math.Round(v * UiScale * DeviceDpi / 96.0);

    private void EnsureFonts()
    {
        double sc = UiScale;
        if (_fontBody != null && Math.Abs(sc - _fontScale) < 0.01) return;
        _fontScale = sc;
        var a = _fontBody; var b = _fontSmall;
        _fontBody = new Font("Microsoft YaHei UI", (float)(9.5 * sc), FontStyle.Regular, GraphicsUnit.Point);
        _fontSmall = new Font("Microsoft YaHei UI", (float)(8.5 * sc), FontStyle.Regular, GraphicsUnit.Point);
        a?.Dispose(); b?.Dispose();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExNoActivate | WsExToolWindow;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyRoundRegion();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyRoundRegion();
        EnsureFonts();
        Invalidate();
    }

    private void ApplyRoundRegion()
    {
        if (Width <= 0 || Height <= 0) return;
        int r = Math.Max(6, P(12));
        using var path = new GraphicsPath();
        var b = new Rectangle(0, 0, Width, Height);
        path.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90);
        path.AddArc(b.Right - r * 2 - 1, b.Y, r * 2, r * 2, 270, 90);
        path.AddArc(b.Right - r * 2 - 1, b.Bottom - r * 2 - 1, r * 2, r * 2, 0, 90);
        path.AddArc(b.X, b.Bottom - r * 2 - 1, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        var region = new System.Drawing.Region(path);
        var old = Region;
        Region = region;
        old?.Dispose();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        EnsureFonts();
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var text = ParseColor(_cfg.TextColor, Color.FromArgb(238, 242, 248));
        var accent = ParseColor(_cfg.AccentColor, Color.FromArgb(255, 197, 61));
        var dim = ParseColor(_cfg.DimColor, Color.FromArgb(124, 132, 150));

        using (var pen = new Pen(Color.FromArgb(46, 255, 255, 255)))
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

        var s = _snap;
        int pad = P(14), icon = P(31), gap = P(7), colX = P(152), row1 = P(12), row2 = P(55), row3 = P(89);

        // 第一行：币 + 次数 + 百分比
        int x1 = pad, x2 = colX;
        DrawCoin(g, _coinFront, x1, row1, icon);
        int cy = row1 + icon / 2;
        int w = DrawText(g, s.Front.ToString(), x1 + icon + gap, cy, _fontBody, dim, true);
        DrawText(g, Pct(s.Front, s.Total), x1 + icon + gap + w + gap, cy, _fontBody, accent, true);

        DrawCoin(g, _coinBack, x2, row1, icon);
        w = DrawText(g, s.Back.ToString(), x2 + icon + gap, cy, _fontBody, dim, true);
        DrawText(g, Pct(s.Back, s.Total), x2 + icon + gap + w + gap, cy, _fontBody, accent, true);

        // 第二行：先手 / 后手
        DrawText(g, "先手 " + s.First, x1, row2, _fontBody, text, false);
        DrawText(g, "后手 " + s.Second, x2, row2, _fontBody, text, false);

        // 第三行：今日运势
        int lw = DrawText(g, "今日运势：", x1, row3, _fontSmall, dim, false);
        DrawClipped(g, s.Fortune, x1 + lw, row3, Width - x1 - lw - pad, _fontSmall, accent);

        // 状态点
        var dot = s.Status.StartsWith("已锁定") ? Color.FromArgb(90, 220, 130)
                 : s.Status.StartsWith("对局中") ? Color.FromArgb(255, 197, 61)
                 : Color.FromArgb(110, 118, 134);
        int d = Math.Max(5, P(8));
        using (var b = new SolidBrush(dot))
            g.FillEllipse(b, Width - pad - d, P(14), d, d);

        // 右下角缩放示意
        using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), Math.Max(1f, P(1))))
        {
            int gx = Width - P(5), gy = Height - P(5), step = P(5);
            for (int i = 1; i <= 3; i++)
                g.DrawLine(pen, gx - i * step, gy, gx, gy - i * step);
        }
    }

    private void DrawCoin(Graphics g, Image? img, int x, int y, int size)
    {
        if (img != null) g.DrawImage(img, new Rectangle(x, y, size, size));
    }

    private static string Pct(int n, int total) => total == 0 ? "--" : Math.Round(100.0 * n / total) + "%";

    /// <summary>画一行字，返回宽度。</summary>
    private int DrawText(Graphics g, string s, int x, int y, Font f, Color c, bool middle)
    {
        var size = TextRenderer.MeasureText(g, s, f);
        TextRenderer.DrawText(g, s, f, new Point(x, middle ? y - size.Height / 2 : y), c, TextFormatFlags.NoPadding);
        return size.Width;
    }

    private void DrawClipped(Graphics g, string s, int x, int y, int maxWidth, Font f, Color c)
    {
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(g, s, f, new Rectangle(x, y, Math.Max(P(30), maxWidth), P(24)), c, flags);
    }

    // ---- 鼠标：中间拖动移动，边缘拖动缩放 ----
    private Zone HitZone(Point p)
    {
        int m = Math.Max(4, P(7));
        bool l = p.X <= m, r = p.X >= Width - m, t = p.Y <= m, b = p.Y >= Height - m;
        if (l && t) return Zone.TL;
        if (r && t) return Zone.TR;
        if (l && b) return Zone.BL;
        if (r && b) return Zone.BR;
        if (l) return Zone.L;
        if (r) return Zone.R;
        if (t) return Zone.T;
        if (b) return Zone.B;
        return Zone.None;
    }

    private static Cursor CursorFor(Zone z) => z switch
    {
        Zone.L or Zone.R => Cursors.SizeWE,
        Zone.T or Zone.B => Cursors.SizeNS,
        Zone.TL or Zone.BR => Cursors.SizeNWSE,
        Zone.TR or Zone.BL => Cursors.SizeNESW,
        _ => Cursors.SizeAll
    };

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;

        _dragCursor = Cursor.Position;
        _dragRect = Bounds;
        _zone = HitZone(new Point(e.X, e.Y));
        _resizing = _zone != Zone.None && !_locked;
        _moving = !_resizing;
        if (!_locked) Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_locked) return;

        if (!_moving && !_resizing)
        {
            Cursor = CursorFor(HitZone(new Point(e.X, e.Y)));
            return;
        }

        var cur = Cursor.Position;
        if (_resizing) DoResize(cur);
        else Location = new Point(_dragRect.Left + cur.X - _dragCursor.X, _dragRect.Top + cur.Y - _dragCursor.Y);
    }

    private void DoResize(Point cur)
    {
        int dx = cur.X - _dragCursor.X, dy = cur.Y - _dragCursor.Y;
        var z = _zone;
        double bw = _dragRect.Width, bh = _dragRect.Height;

        bool vertical = z is Zone.T or Zone.B;
        double delta = vertical ? dy * bw / bh : dx;
        if (z is Zone.T or Zone.TL or Zone.TR) delta = -dy * bw / bh;
        else if (z is Zone.L or Zone.TL or Zone.BL) delta = -dx;

        // 角手柄两个方向都算，取变化大的那个
        if (z is Zone.TL or Zone.TR or Zone.BL or Zone.BR)
        {
            double d = (z is Zone.TL or Zone.TR) ? -dy * bw / bh : dy * bw / bh;
            if (Math.Abs(d) > Math.Abs(delta)) delta = d;
        }

        double newW = Math.Clamp(bw + delta, S(240), S(720));
        double newH = newW * BaseHeight / BaseWidth;

        int left = _dragRect.Left, top = _dragRect.Top;
        if (z is Zone.L or Zone.TL or Zone.BL) left = (int)Math.Round(_dragRect.Right - newW);
        if (z is Zone.T or Zone.TL or Zone.TR) top = (int)Math.Round(_dragRect.Bottom - newH);

        Bounds = new Rectangle(left, top, (int)Math.Round(newW), (int)Math.Round(newH));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_moving && !_resizing) return;
        _moving = _resizing = false;
        _zone = Zone.None;
        Capture = false;
        SavePlacement();
    }

    private void SavePlacement()
    {
        _cfg.X = Left;
        _cfg.Y = Top;
        _cfg.Width = Math.Clamp((int)Math.Round(Width * 96.0 / DeviceDpi), 240, 720);
        _cfg.Save();
    }

    // ---- 右键菜单 ----
    private void BuildMenu()
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false };

        var textColor = new ToolStripMenuItem("文字颜色");
        foreach (var (name, hex) in new[] { ("浅白", "#EEF2F8"), ("浅黄", "#F5E6A8"), ("浅青", "#A8E6E0"), ("浅粉", "#F5C2D0") })
        {
            var hexv = hex;
            textColor.DropDownItems.Add(name, null, (_, _) => { _cfg.TextColor = hexv; _cfg.Save(); Invalidate(); });
        }
        textColor.DropDownItems.Add(new ToolStripSeparator());
        textColor.DropDownItems.Add("自定义…", null, (_, _) => PickColor(c => _cfg.TextColor = c));

        var accentColor = new ToolStripMenuItem("强调色");
        foreach (var (name, hex) in new[] { ("金色", "#FFC53D"), ("青色", "#5FD3C4"), ("粉色", "#F58BA8"), ("绿色", "#7FD87F") })
        {
            var hexv = hex;
            accentColor.DropDownItems.Add(name, null, (_, _) => { _cfg.AccentColor = hexv; _cfg.Save(); Invalidate(); });
        }
        accentColor.DropDownItems.Add(new ToolStripSeparator());
        accentColor.DropDownItems.Add("自定义…", null, (_, _) => PickColor(c => _cfg.AccentColor = c));

        var opacity = new ToolStripMenuItem("不透明度");
        foreach (var v in new[] { 100, 92, 80, 65, 50 })
        {
            int val = v;
            opacity.DropDownItems.Add(val + "%", null, (_, _) => { _cfg.Opacity = val; _cfg.Save(); Opacity = val / 100.0; });
        }

        var size = new ToolStripMenuItem("大小");
        foreach (var (name, w) in new[] { ("小", 240), ("标准", 300), ("大", 400), ("特大", 520) })
        {
            int val = w;
            size.DropDownItems.Add(name, null, (_, _) => SetLogicalWidth(val));
        }

        var lockItem = new ToolStripMenuItem("锁定位置与大小") { CheckOnClick = true };
        lockItem.CheckedChanged += (_, _) => _locked = lockItem.Checked;

        menu.Items.Add(textColor);
        menu.Items.Add(accentColor);
        menu.Items.Add(opacity);
        menu.Items.Add(size);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(lockItem);
        menu.Items.Add("打开记录文件", null, (_, _) => OpenPath(_cfg.Csv));
        menu.Items.Add("打开配置与日志", null, (_, _) => OpenPath(AppContext.BaseDirectory));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Close());

        ContextMenuStrip = menu;
    }

    private void SetLogicalWidth(int logical)
    {
        int w = S(Math.Clamp(logical, 240, 720));
        var b = Bounds;
        int right = b.Right, bottom = b.Bottom;
        int h = (int)Math.Round(w * BaseHeight / BaseWidth);
        Bounds = new Rectangle(right - w, b.Top, w, h);
        SavePlacement();
    }

    private void PickColor(Action<string> apply)
    {
        using var dlg = new ColorDialog { FullOpen = true, AnyColor = true };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        apply("#" + dlg.Color.R.ToString("X2") + dlg.Color.G.ToString("X2") + dlg.Color.B.ToString("X2"));
        _cfg.Save();
        Invalidate();
    }

    private static void OpenPath(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception e) { Log.Write("打开失败 " + path + ": " + e.Message); }
    }

    internal static Color ParseColor(string hex, Color fallback)
    {
        try
        {
            var h = hex.TrimStart('#');
            if (h.Length != 6) return fallback;
            return Color.FromArgb(Convert.ToInt32(h.Substring(0, 2), 16),
                                  Convert.ToInt32(h.Substring(2, 2), 16),
                                  Convert.ToInt32(h.Substring(4, 2), 16));
        }
        catch { return fallback; }
    }

    private static Image? LoadAsset(string name)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var res = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(name, StringComparison.OrdinalIgnoreCase));
            if (res == null) return null;
            using var s = asm.GetManifestResourceStream(res)!;
            var ms = new MemoryStream();
            s.CopyTo(ms);
            ms.Position = 0;
            return Image.FromStream(ms);
        }
        catch (Exception e) { Log.Write("加载图标失败 " + name + ": " + e.Message); return null; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tick?.Dispose();
            _fontBody?.Dispose();
            _fontSmall?.Dispose();
        }
        base.Dispose(disposing);
    }
}
