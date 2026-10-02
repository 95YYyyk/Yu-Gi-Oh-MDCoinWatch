using System.Drawing;

namespace MdCoinWatch;

internal sealed class Detector : IDisposable
{
    private readonly TemplateSet _ts;
    private readonly Options _o;
    private readonly Region _btnA, _btnB, _choice, _wait, _kw, _end;

    /// <summary>最近一次投币检测里三块区域的最大灰度极差；接近 0 说明抓到的是纯色。</summary>
    internal int LastCoinContrast { get; private set; }

    internal Detector(IntPtr screenDc, TemplateSet ts, double sx, double sy, Options o)
    {
        _ts = ts; _o = o;
        _btnA = Box(screenDc, Geometry.BtnFirst, sx, sy, 0);
        _btnB = Box(screenDc, Geometry.BtnSecond, sx, sy, 0);
        _choice = Box(screenDc, Geometry.ChoiceLine, sx, sy, Geometry.Margin);
        _wait = Box(screenDc, Geometry.WaitLine, sx, sy, Geometry.Margin);
        _kw = Box(screenDc, Geometry.KwBox, sx, sy, Geometry.Margin);
        _end = Box(screenDc, Geometry.EndRow, sx, sy, Geometry.Margin);
    }

    private static int S(int v, double s) => (int)Math.Round(v * s);

    private static Region Box(IntPtr dc, (int x, int y, int w, int h) r, double sx, double sy, int pad)
    {
        var b = Geometry.ScaledBox(r, sx, sy, pad);
        return new Region(dc, b.x, b.y, b.w, b.h);
    }

    /// <summary>投币胜负：先看两个按钮在不在，在就核对选择行，不在就核对等待行。</summary>
    internal Coin DetectCoin(int ox, int oy, out int yA, out int yB, out double choiceScore, out double waitScore)
    {
        _btnA.Grab(ox, oy);
        _btnB.Grab(ox, oy);
        yA = _btnA.CountYellow(190, 150, 140, 80);
        yB = _btnB.CountYellow(190, 150, 140, 80);
        choiceScore = -1; waitScore = -1;
        Region line;

        if (yA >= _o.ButtonYellowMin && yB >= _o.ButtonYellowMin)
        {
            _choice.Grab(ox, oy);
            choiceScore = _choice.Match(_ts.ChoiceLine, Geometry.SearchStep, out _, out _);
            line = _choice;
        }
        else
        {
            _wait.Grab(ox, oy);
            waitScore = _wait.Match(_ts.WaitLine, Geometry.SearchStep, out _, out _);
            line = _wait;
        }

        LastCoinContrast = Math.Max(Math.Max(_btnA.Contrast(), _btnB.Contrast()), line.Contrast());
        return Core.DecideCoin(yA, yB, choiceScore, waitScore, _o);
    }

    internal Turn DetectTurn(int ox, int oy, out double sFirst, out double sSecond)
    {
        _kw.Grab(ox, oy);
        sFirst = _kw.Match(_ts.KwFirst, Geometry.SearchStep, out _, out _);
        sSecond = _kw.Match(_ts.KwSecond, Geometry.SearchStep, out _, out _);
        return Core.DecideTurn(sFirst, sSecond, _o);
    }

    internal bool DetectEnd(int ox, int oy, out double score)
    {
        _end.Grab(ox, oy);
        score = _end.Match(_ts.EndRow, Geometry.SearchStep, out _, out _);
        return score >= _o.MatchThreshold;
    }

    /// <summary>浮窗是画在屏幕上的，压住哪块识别区哪块就读错。这里检查一下。
    /// </summary>
    internal bool Overlaps(int ox, int oy, Rectangle widget, out string what)
    {
        foreach (var (reg, name) in new[]
                 {
                     (_btnA, "投币按钮"), (_btnB, "投币按钮"), (_kw, "先后手横幅"),
                     (_choice, "选择行"), (_wait, "等待行"), (_end, "结束画面")
                 })
        {
            var r = new Rectangle(ox + reg.X, oy + reg.Y, reg.W, reg.H);
            if (r.IntersectsWith(widget)) { what = name; return true; }
        }
        what = "";
        return false;
    }

    internal void Dump(string dir, string tag)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("MMdd_HHmmss_fff");
            _btnA.SaveBmp(Path.Combine(dir, stamp + "_" + tag + "_btnA.bmp"));
            _btnB.SaveBmp(Path.Combine(dir, stamp + "_" + tag + "_btnB.bmp"));
            _choice.SaveBmp(Path.Combine(dir, stamp + "_" + tag + "_choice.bmp"));
            _wait.SaveBmp(Path.Combine(dir, stamp + "_" + tag + "_wait.bmp"));
            _kw.SaveBmp(Path.Combine(dir, stamp + "_" + tag + "_kw.bmp"));
            _end.SaveBmp(Path.Combine(dir, stamp + "_" + tag + "_end.bmp"));
        }
        catch (Exception e) { Console.WriteLine("[dump 失败] " + e.Message); }
    }

    public void Dispose()
    {
        _btnA.Dispose(); _btnB.Dispose(); _choice.Dispose();
        _wait.Dispose(); _kw.Dispose(); _end.Dispose();
    }
}
