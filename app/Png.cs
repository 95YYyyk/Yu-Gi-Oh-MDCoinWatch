using System.IO.Compression;
using System.Text;

namespace MdCoinWatch;

/// <summary>只为离线自检服务的最小 PNG 解码器：8 位、非隔行、RGB/RGBA/灰度。</summary>
internal static class Png
{
    internal static (byte[] rgb, int w, int h) Load(string path)
    {
        var d = File.ReadAllBytes(path);
        if (d.Length < 8 || d[0] != 0x89 || d[1] != (byte)'P' || d[2] != (byte)'N' || d[3] != (byte)'G')
            throw new InvalidDataException("不是 PNG");

        int w = 0, h = 0, bitDepth = 0, colorType = 0, interlace = 0;
        var idat = new MemoryStream();
        int p = 8;
        while (p + 8 <= d.Length)
        {
            int len = BE(d, p);
            var type = Encoding.ASCII.GetString(d, p + 4, 4);
            int body = p + 8;
            if (body + len > d.Length) break;
            if (type == "IHDR")
            {
                w = BE(d, body); h = BE(d, body + 4);
                bitDepth = d[body + 8]; colorType = d[body + 9]; interlace = d[body + 12];
            }
            else if (type == "IDAT") idat.Write(d, body, len);
            else if (type == "IEND") break;
            p = body + len + 4;
        }
        if (w <= 0 || h <= 0) throw new InvalidDataException("IHDR 损坏");
        if (bitDepth != 8) throw new NotSupportedException("只支持 8 位 PNG");
        if (interlace != 0) throw new NotSupportedException("不支持隔行 PNG");
        int ch = colorType switch
        {
            0 => 1, 2 => 3, 4 => 2, 6 => 4,
            _ => throw new NotSupportedException("不支持的颜色类型 " + colorType)
        };

        idat.Position = 0;
        int stride = w * ch;
        var raw = new byte[(stride + 1) * h];
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
        {
            int off = 0;
            while (off < raw.Length)
            {
                int r = z.Read(raw, off, raw.Length - off);
                if (r <= 0) break;
                off += r;
            }
        }

        var img = new byte[stride * h];
        for (int y = 0, pp = 0, cur = 0; y < h; y++, pp += stride + 1, cur += stride)
        {
            int filter = raw[pp];
            for (int x = 0; x < stride; x++)
            {
                int a = x >= ch ? img[cur + x - ch] : 0;
                int b = y > 0 ? img[cur - stride + x] : 0;
                int c = (x >= ch && y > 0) ? img[cur - stride + x - ch] : 0;
                int v = raw[pp + 1 + x];
                v = filter switch
                {
                    0 => v,
                    1 => v + a,
                    2 => v + b,
                    3 => v + ((a + b) >> 1),
                    4 => v + Paeth(a, b, c),
                    _ => throw new InvalidDataException("未知滤波器 " + filter)
                };
                img[cur + x] = (byte)v;
            }
        }

        var rgb = new byte[w * h * 3];
        for (int i = 0, s = 0, t = 0; i < w * h; i++, s += ch, t += 3)
        {
            if (ch <= 2) { rgb[t] = rgb[t + 1] = rgb[t + 2] = img[s]; }
            else { rgb[t] = img[s]; rgb[t + 1] = img[s + 1]; rgb[t + 2] = img[s + 2]; }
        }
        return (rgb, w, h);
    }

    /// <summary>双线性缩放到目标尺寸，用来离线模拟别的分辨率下的客户区。</summary>
    internal static byte[] Resize(byte[] rgb, int w, int h, int nw, int nh)
    {
        if (nw == w && nh == h) return rgb;
        var o = new byte[nw * nh * 3];
        for (int y = 0; y < nh; y++)
        {
            double fy0 = (y + 0.5) * h / nh - 0.5;
            int y0 = (int)Math.Floor(fy0);
            double fy = fy0 - y0;
            int y1 = Math.Clamp(y0 + 1, 0, h - 1);
            y0 = Math.Clamp(y0, 0, h - 1);
            for (int x = 0; x < nw; x++)
            {
                double fx0 = (x + 0.5) * w / nw - 0.5;
                int x0 = (int)Math.Floor(fx0);
                double fx = fx0 - x0;
                int x1 = Math.Clamp(x0 + 1, 0, w - 1);
                x0 = Math.Clamp(x0, 0, w - 1);
                int a = (y0 * w + x0) * 3, b = (y0 * w + x1) * 3;
                int c = (y1 * w + x0) * 3, d = (y1 * w + x1) * 3;
                int t = (y * nw + x) * 3;
                for (int k = 0; k < 3; k++)
                {
                    double top = rgb[a + k] + (rgb[b + k] - rgb[a + k]) * fx;
                    double bot = rgb[c + k] + (rgb[d + k] - rgb[c + k]) * fx;
                    o[t + k] = (byte)Math.Clamp(top + (bot - top) * fy, 0.0, 255.0);
                }
            }
        }
        return o;
    }

    private static int Paeth(int a, int b, int c)
    {
        int q = a + b - c, pa = Math.Abs(q - a), pb = Math.Abs(q - b), pc = Math.Abs(q - c);
        if (pa <= pb && pa <= pc) return a;
        return pb <= pc ? b : c;
    }

    private static int BE(byte[] d, int i) => (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];
}
