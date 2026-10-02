using System.Reflection;

namespace MdCoinWatch;

/// <summary>1920x1080 基准下的固定坐标。真实坐标由窗口缩放比换算。</summary>
internal static class Geometry
{
    internal const int RefW = 1920;
    internal const int RefH = 1080;
    internal const int Margin = 4;

    /// <summary>模板搜索步长。必须是 1：偶数步长到不了奇数像素偏移，1 px 偏差就能把分数打到阈值以下。</summary>
    internal const int SearchStep = 1;

    internal static readonly (int x, int y, int w, int h) BtnFirst = (725, 822, 110, 36);
    internal static readonly (int x, int y, int w, int h) BtnSecond = (1105, 822, 110, 36);
    internal static readonly (int x, int y, int w, int h) KwBox = (930, 690, 100, 52);
    internal static readonly (int x, int y, int w, int h) ChoiceLine = (780, 698, 360, 44);
    internal static readonly (int x, int y, int w, int h) WaitLine = (680, 698, 560, 44);
    internal static readonly (int x, int y, int w, int h) EndRow = (690, 775, 555, 125);

    internal static int S(int v, double s) => (int)Math.Round(v * s);

    /// <summary>把基准坐标换算成实际客户区坐标。实时抓屏和离线自检都走这里。</summary>
    internal static (int x, int y, int w, int h) ScaledBox((int x, int y, int w, int h) r, double sx, double sy, int pad)
        => (S(r.x - pad, sx), S(r.y - pad, sy),
            Math.Max(6, S(r.w + 2 * pad, sx)), Math.Max(6, S(r.h + 2 * pad, sy)));
}

internal sealed class Template
{
    internal readonly string Name;
    internal readonly int W, H;
    internal readonly float[] Gray;
    internal readonly double Sum, SumSq;

    private Template(string name, int w, int h, float[] g)
    {
        Name = name; W = w; H = h; Gray = g;
        double s = 0, s2 = 0;
        for (int i = 0; i < g.Length; i++) { s += g[i]; s2 += (double)g[i] * g[i]; }
        Sum = s; SumSq = s2;
    }

    internal static Template FromBytes(string name, int w, int h, byte[] b)
    {
        var f = new float[b.Length];
        for (int i = 0; i < b.Length; i++) f[i] = b[i];
        return new Template(name, w, h, f);
    }

    /// <summary>换分辨率时按比例重采样一次，之后每帧都是定点比较。</summary>
    internal Template Scaled(double sx, double sy)
    {
        int nw = Math.Max(3, (int)Math.Round(W * sx));
        int nh = Math.Max(3, (int)Math.Round(H * sy));
        if (nw == W && nh == H) return this;
        var g = new float[nw * nh];
        for (int y = 0; y < nh; y++)
        {
            double fy0 = (y + 0.5) * H / nh - 0.5;
            int y0 = (int)Math.Floor(fy0);
            double fy = fy0 - y0;
            int y1 = Math.Clamp(y0 + 1, 0, H - 1);
            y0 = Math.Clamp(y0, 0, H - 1);
            for (int x = 0; x < nw; x++)
            {
                double fx0 = (x + 0.5) * W / nw - 0.5;
                int x0 = (int)Math.Floor(fx0);
                double fx = fx0 - x0;
                int x1 = Math.Clamp(x0 + 1, 0, W - 1);
                x0 = Math.Clamp(x0, 0, W - 1);
                double top = Gray[y0 * W + x0] + (Gray[y0 * W + x1] - Gray[y0 * W + x0]) * fx;
                double bot = Gray[y1 * W + x0] + (Gray[y1 * W + x1] - Gray[y1 * W + x0]) * fx;
                g[y * nw + x] = (float)(top + (bot - top) * fy);
            }
        }
        return new Template(Name, nw, nh, g);
    }
}

internal sealed class TemplateSet
{
    internal readonly Template KwFirst, KwSecond, ChoiceLine, WaitLine, EndRow;

    private TemplateSet(Template a, Template b, Template c, Template d, Template e)
    { KwFirst = a; KwSecond = b; ChoiceLine = c; WaitLine = d; EndRow = e; }

    internal static TemplateSet Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        var names = asm.GetManifestResourceNames();

        Template Get(string key, int w, int h)
        {
            var res = names.FirstOrDefault(n => n.EndsWith(key + ".bin", StringComparison.OrdinalIgnoreCase))
                      ?? throw new InvalidOperationException("缺少内嵌模板: " + key);
            using var s = asm.GetManifestResourceStream(res)!;
            var buf = new byte[w * h];
            int off = 0;
            while (off < buf.Length)
            {
                int r = s.Read(buf, off, buf.Length - off);
                if (r <= 0) break;
                off += r;
            }
            if (off != w * h) throw new InvalidOperationException("模板尺寸不符: " + key + " 期望 " + (w * h) + " 实际 " + off);
            return Template.FromBytes(key, w, h, buf);
        }

        return new TemplateSet(
            Get("kw_first", Geometry.KwBox.w, Geometry.KwBox.h),
            Get("kw_second", Geometry.KwBox.w, Geometry.KwBox.h),
            Get("choice_line", Geometry.ChoiceLine.w, Geometry.ChoiceLine.h),
            Get("wait_line", Geometry.WaitLine.w, Geometry.WaitLine.h),
            Get("end_row", Geometry.EndRow.w, Geometry.EndRow.h));
    }

    internal TemplateSet Scaled(double sx, double sy) => new(
        KwFirst.Scaled(sx, sy), KwSecond.Scaled(sx, sy),
        ChoiceLine.Scaled(sx, sy), WaitLine.Scaled(sx, sy), EndRow.Scaled(sx, sy));
}
