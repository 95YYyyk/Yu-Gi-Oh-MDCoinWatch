using System.Runtime.InteropServices;

namespace MdCoinWatch;

/// <summary>离屏 32bpp DIB。抓屏直接 BitBlt 进它自己的内存，不产生托管分配。</summary>
internal sealed unsafe class Surface : IDisposable
{
    private readonly IntPtr _dc, _bmp, _old, _screenDc;
    internal readonly byte* Bits;
    internal readonly int W, H;

    internal Surface(IntPtr screenDc, int w, int h)
    {
        _screenDc = screenDc;
        W = w; H = h;
        _dc = Win32.CreateCompatibleDC(screenDc);
        var bmi = new Win32.BITMAPINFO();
        bmi.bmiHeader.biSize = Marshal.SizeOf<Win32.BITMAPINFOHEADER>();
        bmi.bmiHeader.biWidth = w;
        bmi.bmiHeader.biHeight = -h;
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = 0;
        _bmp = Win32.CreateDIBSection(screenDc, ref bmi, 0, out var bits, IntPtr.Zero, 0);
        if (_bmp == IntPtr.Zero || bits == IntPtr.Zero) throw new InvalidOperationException("CreateDIBSection 失败");
        Bits = (byte*)bits;
        _old = Win32.SelectObject(_dc, _bmp);
    }

    internal bool Grab(int x, int y) => Win32.BitBlt(_dc, 0, 0, W, H, _screenDc, x, y, Win32.SRCCOPY);

    public void Dispose()
    {
        if (_old != IntPtr.Zero) Win32.SelectObject(_dc, _old);
        if (_bmp != IntPtr.Zero) Win32.DeleteObject(_bmp);
        if (_dc != IntPtr.Zero) Win32.DeleteDC(_dc);
    }
}

/// <summary>屏幕上的一个固定矩形：抓取 -> 灰度 -> 匹配。</summary>
internal sealed unsafe class Region : IDisposable
{
    internal readonly int X, Y, W, H;
    internal readonly byte[] Gray;
    private readonly Surface _s;
    private double[] _i1 = Array.Empty<double>();
    private double[] _i2 = Array.Empty<double>();

    internal Region(IntPtr screenDc, int x, int y, int w, int h)
    {
        X = x; Y = y; W = w; H = h;
        Gray = new byte[w * h];
        _s = new Surface(screenDc, w, h);
    }

    internal void Grab(int originX, int originY)
    {
        _s.Grab(originX + X, originY + Y);
        var b = _s.Bits;
        var g = Gray;
        int n = W * H;
        for (int i = 0, p = 0; i < n; i++, p += 4)
            g[i] = (byte)((b[p + 2] * 299 + b[p + 1] * 587 + b[p] * 114) / 1000);
    }

    internal int CountYellow(int minR, int minG, int maxB, int minRB)
    {
        var b = _s.Bits;
        int n = W * H, c = 0;
        for (int i = 0, p = 0; i < n; i++, p += 4)
        {
            int r = b[p + 2], g = b[p + 1], bl = b[p];
            if (r > minR && g > minG && bl < maxB && r - bl > minRB) c++;
        }
        return c;
    }

    /// <summary>灰度极差，用来判断这块区域是不是一片纯色（黑屏检测）。</summary>
    internal int Contrast()
    {
        var g = Gray;
        int lo = 255, hi = 0;
        for (int i = 0; i < g.Length; i++)
        {
            int v = g[i];
            if (v < lo) lo = v;
            if (v > hi) hi = v;
        }
        return g.Length == 0 ? 0 : hi - lo;
    }

    internal double Match(Template t, int step, out int bx, out int by)
        => Core.Ncc(Gray, W, H, t, step, ref _i1, ref _i2, out bx, out by);

    /// <summary>调试用：把这块区域存成 BMP（BGRA 原样）。</summary>
    internal void SaveBmp(string path)
    {
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        int rowBytes = W * 4;
        w.Write((byte)'B'); w.Write((byte)'M');
        w.Write(54 + rowBytes * H); w.Write(0); w.Write(54);
        w.Write(40); w.Write(W); w.Write(H); w.Write((short)1); w.Write((short)32);
        w.Write(0); w.Write(rowBytes * H); w.Write(2835); w.Write(2835); w.Write(0); w.Write(0);
        var b = _s.Bits;
        for (int y = H - 1; y >= 0; y--)
            for (int x = 0; x < W; x++)
            {
                int p = (y * W + x) * 4;
                w.Write(b[p]); w.Write(b[p + 1]); w.Write(b[p + 2]); w.Write((byte)255);
            }
    }

    public void Dispose() => _s.Dispose();
}
