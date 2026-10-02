namespace MdCoinWatch;

/// <summary>纯计算部分。不碰屏幕，离线自检和实时识别走同一份代码。</summary>
internal static class Core
{
    /// <summary>
    /// TM_CCOEFF_NORMED。gray 是 gw x gh 灰度图，模板在其中滑动取最高分。
    /// 先用 coarse 步长粗搜，再在粗搜最优点周围按 step 精搜，这样既能覆盖奇数像素偏移，
    /// 又不用把所有位置都算一遍。
    /// </summary>
    internal static double Ncc(byte[] gray, int gw, int gh, Template t, int step,
                               ref double[] i1, ref double[] i2, out int bx, out int by)
    {
        bx = by = 0;
        int tw = t.W, th = t.H;
        int maxX = gw - tw, maxY = gh - th;
        if (maxX < 0 || maxY < 0) return double.NegativeInfinity;

        int iw = gw + 1, ih = gh + 1;
        if (i1.Length < iw * ih) { i1 = new double[iw * ih]; i2 = new double[iw * ih]; }
        double[] I1 = i1, I2 = i2;          // 本地函数抓不了 ref 参数，先落到局部变量
        for (int x = 0; x <= gw; x++) { I1[x] = 0; I2[x] = 0; }
        for (int y = 0; y <= gh; y++) { I1[y * iw] = 0; I2[y * iw] = 0; }
        for (int y = 0; y < gh; y++)
        {
            double r1 = 0, r2 = 0;
            int row = y * gw, iRow = (y + 1) * iw, iPrev = y * iw;
            for (int x = 0; x < gw; x++)
            {
                double v = gray[row + x];
                r1 += v; r2 += v * v;
                I1[iRow + x + 1] = I1[iPrev + x + 1] + r1;
                I2[iRow + x + 1] = I2[iPrev + x + 1] + r2;
            }
        }

        int n = tw * th;
        double sumT = t.Sum;
        double varT = t.SumSq - sumT * sumT / n;
        if (varT <= 1e-9) return double.NegativeInfinity;
        var tg = t.Gray;

        double Eval(int x0, int y0)
        {
            int a = y0 * iw + x0, b = (y0 + th) * iw + x0;
            int c = a + tw, d = b + tw;
            double sumI = I1[d] - I1[c] - I1[b] + I1[a];
            double varI = (I2[d] - I2[c] - I2[b] + I2[a]) - sumI * sumI / n;
            if (varI <= 1e-9) return double.NegativeInfinity;

            double sumTI = 0;
            for (int ty = 0; ty < th; ty++)
            {
                int row = (y0 + ty) * gw + x0;
                int trow = ty * tw;
                double acc = 0;
                for (int tx = 0; tx < tw; tx++) acc += gray[row + tx] * tg[trow + tx];
                sumTI += acc;
            }
            return (sumTI - sumT * sumI / n) / Math.Sqrt(varT * varI);
        }

        int coarse = Math.Max(step, 2);
        double best = double.NegativeInfinity;
        for (int y = 0; y <= maxY; y += coarse)
            for (int x = 0; x <= maxX; x += coarse)
            {
                double s = Eval(x, y);
                if (s > best) { best = s; bx = x; by = y; }
            }

        if (step < coarse)
        {
            int cx = bx, cy = by;
            for (int y = Math.Max(0, cy - coarse + step); y <= Math.Min(maxY, cy + coarse - step); y += step)
                for (int x = Math.Max(0, cx - coarse + step); x <= Math.Min(maxX, cx + coarse - step); x += step)
                {
                    if ((x - cx) % coarse == 0 && (y - cy) % coarse == 0) continue;   // 粗搜已经算过
                    double s = Eval(x, y);
                    if (s > best) { best = s; bx = x; by = y; }
                }
        }
        return best;
    }

    /// <summary>数 bgra 缓冲里有多少个"按钮文字那种黄"的像素。</summary>
    internal static int CountYellow(byte[] bgra, int minR, int minG, int maxB, int minRB)
    {
        int c = 0;
        for (int p = 0; p + 3 < bgra.Length; p += 4)
        {
            int b = bgra[p], g = bgra[p + 1], r = bgra[p + 2];
            if (r > minR && g > minG && b < maxB && r - b > minRB) c++;
        }
        return c;
    }

    internal static Coin DecideCoin(int yA, int yB, double choiceScore, double waitScore, Options o)
    {
        if (yA >= o.ButtonYellowMin && yB >= o.ButtonYellowMin)
            return choiceScore >= o.MatchThreshold ? Coin.Win : Coin.None;
        return waitScore >= o.MatchThreshold ? Coin.Lose : Coin.None;
    }

    internal static Turn DecideTurn(double sFirst, double sSecond, Options o)
    {
        if (Math.Max(sFirst, sSecond) < o.TurnThreshold) return Turn.None;
        return sFirst >= sSecond ? Turn.First : Turn.Second;
    }

    internal static string CoinText(Coin c) => c switch { Coin.Win => "WIN", Coin.Lose => "LOSE", _ => "?" };
    internal static string TurnText(Turn t) => t switch { Turn.First => "FIRST", Turn.Second => "SECOND", _ => "?" };
}

internal enum Coin { None, Win, Lose }
internal enum Turn { None, First, Second }
