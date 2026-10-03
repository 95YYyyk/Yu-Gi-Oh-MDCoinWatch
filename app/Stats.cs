using System.Drawing;

namespace MdCoinWatch;

/// <summary>界面要读的一份快照。识别线程写，界面线程读，全靠锁。</summary>
internal sealed class Snapshot
{
    internal string Status = "等待游戏";
    internal string Window = "";
    internal int Total, Front, Back, First, Second;
    internal int Wins, Losses;            // 今天的胜负
    internal int WinStreak;               // 正数 = 连胜，负数 = 连败
    internal int AllTotal, AllFront;      // 整份 CSV（含前几天）
    internal string LastCoin = "-", LastTurn = "-";
    internal int Streak;
    internal List<bool> Recent = new();
    internal string Fortune = "";

    internal double FrontRate => Total == 0 ? 0 : (double)Front / Total;
    internal double BackRate => Total == 0 ? 0 : (double)Back / Total;

    /// <summary>右键菜单里那行「累计硬币正面率」。</summary>
    internal string AllRateText => AllTotal == 0
        ? "累计硬币正面率  --"
        : "累计硬币正面率  " + Math.Round(100.0 * AllFront / AllTotal) + "%（" + AllFront + "/" + AllTotal + "）";
}

internal sealed class Stats
{
    private struct Rec { internal bool Front, First; internal char Result; }   // Result: 'W' / 'L' / '-'

    private readonly object _gate = new();
    private readonly List<Rec> _today = new();
    private int _allTotal, _allFront;
    private string _status = "等待游戏";
    private string _window = "";
    private string _lastCoin = "-", _lastTurn = "-";

    /// <summary>今天那份存进 _today；AllTotal / AllFront 数的是整份文件，包括前几天的。</summary>
    internal void LoadCsv(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            lock (_gate)
            {
                _today.Clear();
                _allTotal = _allFront = 0;
                foreach (var line in File.ReadAllLines(path))
                {
                    var f = line.Split(',');
                    if (f.Length < 3) continue;
                    if (f[0].Trim().Equals("time", StringComparison.OrdinalIgnoreCase)) continue;   // 表头
                    bool front = f[1].Trim().Equals("WIN", StringComparison.OrdinalIgnoreCase);
                    bool first = f[2].Trim().Equals("FIRST", StringComparison.OrdinalIgnoreCase);
                    _allTotal++;
                    if (front) _allFront++;
                    if (!f[0].StartsWith(today, StringComparison.Ordinal)) continue;
                    _today.Add(new Rec { Front = front, First = first, Result = ParseResult(f) });
                }
                if (_today.Count > 0)
                {
                    var last = _today[^1];
                    _lastCoin = last.Front ? "WIN" : "LOSE";
                    _lastTurn = last.First ? "FIRST" : "SECOND";
                }
            }
        }
        catch (Exception e) { Log.Write("读 CSV 失败: " + e.Message); }
    }

    /// <summary>老 CSV 只有 5 列、第 5 列是备注，所以严格认 WIN / LOSE，认不出就当没判出来。</summary>
    private static char ParseResult(string[] f)
    {
        if (f.Length < 5) return '-';
        var v = f[4].Trim();
        if (v.Equals("WIN", StringComparison.OrdinalIgnoreCase)) return 'W';
        if (v.Equals("LOSE", StringComparison.OrdinalIgnoreCase)) return 'L';
        return '-';
    }

    internal void AddRecord(string coin, string turn)
    {
        lock (_gate)
        {
            bool front = coin == "WIN";
            _today.Add(new Rec { Front = front, First = turn == "FIRST", Result = '-' });
            _lastCoin = coin;
            _lastTurn = turn;
            _allTotal++;
            if (front) _allFront++;
        }
    }

    /// <summary>这局结束时判出的胜负，回填到刚才那条记录上。</summary>
    internal void AddResult(Result r)
    {
        if (r == Result.None) return;
        char ch = r == Result.Win ? 'W' : 'L';
        lock (_gate)
        {
            for (int i = _today.Count - 1; i >= 0; i--)
            {
                if (_today[i].Result != '-') continue;
                var x = _today[i];
                x.Result = ch;
                _today[i] = x;
                return;
            }
        }
    }

    /// <summary>「清除所有记录数据」用：内存里的统计也一起归零。</summary>
    internal void Clear()
    {
        lock (_gate)
        {
            _today.Clear();
            _allTotal = _allFront = 0;
            _lastCoin = _lastTurn = "-";
        }
    }

    /// <summary>浮窗实时位置，识别线程用它判断有没有压住识别区。</summary>
    internal Rectangle WidgetRect;

    internal void SetWidgetRect(Rectangle r) => WidgetRect = r;

    internal void SetStatus(string status, string window = "")
    {
        lock (_gate)
        {
            _status = status;
            if (window.Length > 0) _window = window;
        }
    }

    internal Snapshot Read()
    {
        lock (_gate)
        {
            var s = new Snapshot
            {
                Status = _status,
                Window = _window,
                Total = _today.Count,
                Front = _today.Count(t => t.Front),
                First = _today.Count(t => t.First),
                Wins = _today.Count(t => t.Result == 'W'),
                Losses = _today.Count(t => t.Result == 'L'),
                AllTotal = _allTotal,
                AllFront = _allFront,
                LastCoin = _lastCoin,
                LastTurn = _lastTurn
            };
            s.Back = s.Total - s.Front;
            s.Second = s.Total - s.First;

            // 最近的连续同面
            int streak = 0;
            for (int i = _today.Count - 1; i >= 0; i--)
            {
                bool f = _today[i].Front;
                if (streak == 0) streak = f ? 1 : -1;
                else if (f && streak > 0) streak++;
                else if (!f && streak < 0) streak--;
                else break;
            }
            s.Streak = streak;

            int take = Math.Min(8, _today.Count);
            for (int i = _today.Count - take; i < _today.Count; i++) s.Recent.Add(_today[i].Front);

            // 胜负连击：正数 = 连胜，负数 = 连败，只数已经判出胜负的那一段
            int ws = 0;
            for (int i = _today.Count - 1; i >= 0; i--)
            {
                char r = _today[i].Result;
                if (r == '-') break;
                if (ws == 0) ws = r == 'W' ? 1 : -1;
                else if (r == 'W' && ws > 0) ws++;
                else if (r == 'L' && ws < 0) ws--;
                else break;
            }
            s.WinStreak = ws;

            s.Fortune = Fortune.For(s, DateTime.Now.Hour);
            return s;
        }
    }
}

