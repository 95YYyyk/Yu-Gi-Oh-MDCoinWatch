# -*- coding: utf-8 -*-
"""从 运势文案候选.md 生成 app/Fortune.cs。

改文案的流程：
  1. 改 运势文案候选.md（保持 39 个小节的顺序和写法：### 标题 + 表格 + | 文案 | 字数 | 语气 |）
  2. python tools/gen_fortune.py
  3. dotnet build app/MdCoinWatch.csproj -c Release

脚本只做搬运，不加自己的判断；判定顺序和阈值都在生成出来的 Fortune.For() 里。
"""

import os, re, sys
sys.stdout.reconfigure(encoding='utf-8')

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
md = open(os.path.join(root, '运势文案候选.md'), encoding='utf-8').read()

# 按顺序切出 39 个小节
secs = []
cur = None
for line in md.split('\n'):
    if line.startswith('### '):
        cur = {'title': line[4:].strip(), 'items': []}
        secs.append(cur)
        continue
    if cur is None: continue
    m = re.match(r'^\|\s*([^|]+?)\s*\|\s*\d+\s*\|\s*(燃|损|温和)', line)
    if m: cur['items'].append(m.group(1))

names = (['E%02d' % i for i in range(1, 14)]
         + ['K_P5', 'K_N5', 'K_P3', 'K_N3']
         + ['D0', 'D1']
         + ['M%d%d' % (c, w) for c in range(5, 0, -1) for w in range(5, 2, -1)]
         + ['F%d' % c for c in range(5, 0, -1)])
assert len(names) == len(secs), '小节 %d 个，名字 %d 个' % (len(secs), len(names))
for n, s in zip(names, secs):
    assert s['items'], n + ' 没抓到文案'

out = []
out.append('namespace MdCoinWatch;')
out.append('')
out.append('/// <summary>')
out.append('/// 今日运势文案。优先级：彩蛋 -> 连正/连反 -> 没数据 -> 硬币 × 胜率 5x3 矩阵 -> 单维兜底。')
out.append('/// 文案池在 运势文案候选.md，改完文案跑 tools/gen_fortune.py 重新生成本文件。')
out.append('/// </summary>')
out.append('internal static class Fortune')
out.append('{')
for n, s in zip(names, secs):
    out.append('    // %s' % s['title'])
    out.append('    private static readonly string[] %s =' % n)
    out.append('    {')
    for it in s['items']:
        out.append('        "%s",' % it)
    out.append('    };')
    out.append('')

out.append('    // 硬币 × 胜率：M[硬币档][胜率档]')
out.append('    private static readonly string[][][] M =')
out.append('    {')
for i in range(0, 15, 3):
    out.append('        new[] { ' + ', '.join(names[19 + i:19 + i + 3]) + ' },')
out.append('    };')
out.append('')
out.append('    // 单维兜底：F[硬币档]')
out.append('    private static readonly string[][] F =')
out.append('    {')
out.append('        ' + ', '.join(names[34:39]) + ',')
out.append('    };')
out.append('''
    internal static string For(Snapshot s, int hour)
    {
        int total = s.Total, front = s.Front, streak = s.Streak, wstreak = s.WinStreak;
        int wins = s.Wins, losses = s.Losses, settled = wins + losses;
        double p = total == 0 ? 0 : (double)front / total;
        double wr = settled == 0 ? 0 : (double)wins / settled;

        // ---------------- 彩蛋 ----------------
        if (streak <= -5 && settled >= 5 && losses == 0) return Pick(E01, total);
        if (streak >= 10) return Pick(E02, total);
        if (streak >= 5 && settled >= 5 && wins == 0) return Pick(E03, total);
        if (total == 1 && wins == 1) return Pick(E13, total);
        if (settled >= 4 && losses == 0) return Pick(E04, total);
        if (settled >= 4 && wins == 0) return Pick(E05, total);
        if (total >= 5 && front == total) return Pick(E06, total);
        if (total >= 5 && front == 0) return Pick(E07, total);
        if (total >= 6 && total % 2 == 0 && front * 2 == total) return Pick(E08, total);
        if (wstreak >= 5) return Pick(E09, total);
        if (wstreak <= -5) return Pick(E10, total);
        if (hour >= 0 && hour < 5 && total >= 5) return Pick(E11, total);
        if (total == 10 || total == 20 || total == 30) return Pick(E12, total).Replace("N", total.ToString());

        // ---------------- 连正 / 连反 ----------------
        if (streak >= 5) return Pick(K_P5, total).Replace("N", streak.ToString());
        if (streak <= -5) return Pick(K_N5, total).Replace("N", (-streak).ToString());
        if (streak >= 3) return Pick(K_P3, total).Replace("N", streak.ToString());
        if (streak <= -3) return Pick(K_N3, total).Replace("N", (-streak).ToString());

        // ---------------- 还没数据 ----------------
        if (total == 0) return Pick(D0, total);
        if (total < 3) return Pick(D1, total);

        // ---------------- 硬币 × 胜率 5x3 ----------------
        if (settled >= 3)
        {
            int wi = wr >= 0.60 ? 0 : wr >= 0.40 ? 1 : 2;
            return Pick(M[CoinBand(p)][wi], total);
        }

        // ---------------- 单维兜底（打了但胜负还没判出来）----------------
        return Pick(F[CoinBand(p)], total);
    }

    /// <summary>硬币 5 档：0 = ≥70%，1 = 58~70%，2 = 45~58%，3 = 32~45%，4 = &lt;32%。</summary>
    private static int CoinBand(double p)
        => p >= 0.70 ? 0 : p >= 0.58 ? 1 : p >= 0.45 ? 2 : p >= 0.32 ? 3 : 4;

    /// <summary>
    /// 确定性挑选：用 total 取模，同一个状态永远同一条。
    /// 千万别用随机 —— 界面每 500ms 重算一次，随机会让文案一秒闪两下。
    /// </summary>
    private static string Pick(string[] a, int seed)
        => a.Length == 0 ? "" : a[(seed % a.Length + a.Length) % a.Length];
}
''')

p = os.path.join(root, 'app', 'Fortune.cs')
open(p, 'w', encoding='utf-8', newline='\r\n').write('\n'.join(out))
print('写入 %s' % p)
print('小节 %d 个，文案 %d 条' % (len(secs), sum(len(s['items']) for s in secs)))
for n, s in zip(names, secs):
    print('  %-6s %2d 条  %s' % (n, len(s['items']), s['title']))

