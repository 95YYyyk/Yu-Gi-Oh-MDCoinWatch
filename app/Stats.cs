using System.Drawing;

namespace MdCoinWatch;

/// <summary>界面要读的一份快照。识别线程写，界面线程读，全靠锁。</summary>
internal sealed class Snapshot
{
    internal string Status = "等待游戏";
    internal string Window = "";
    internal int Total, Front, Back, First, Second;
    internal string LastCoin = "-", LastTurn = "-";
    internal int Streak;
    internal List<bool> Recent = new();
    internal string Fortune = "";

    internal double FrontRate => Total == 0 ? 0 : (double)Front / Total;
    internal double BackRate => Total == 0 ? 0 : (double)Back / Total;
}

internal sealed class Stats
{
    private readonly object _gate = new();
    private readonly List<(bool front, bool first)> _today = new();
    private string _status = "等待游戏";
    private string _window = "";
    private string _lastCoin = "-", _lastTurn = "-";

    /// <summary>只统计今天的记录。CSV 里可能有前几天的。</summary>
    internal void LoadCsv(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            lock (_gate)
            {
                _today.Clear();
                foreach (var line in File.ReadAllLines(path))
                {
                    var f = line.Split(',');
                    if (f.Length < 3) continue;
                    if (!f[0].StartsWith(today, StringComparison.Ordinal)) continue;
                    bool front = f[1].Trim().Equals("WIN", StringComparison.OrdinalIgnoreCase);
                    bool first = f[2].Trim().Equals("FIRST", StringComparison.OrdinalIgnoreCase);
                    _today.Add((front, first));
                }
                if (_today.Count > 0)
                {
                    var last = _today[^1];
                    _lastCoin = last.front ? "WIN" : "LOSE";
                    _lastTurn = last.first ? "FIRST" : "SECOND";
                }
            }
        }
        catch (Exception e) { Log.Write("读 CSV 失败: " + e.Message); }
    }

    internal void AddRecord(string coin, string turn)
    {
        lock (_gate)
        {
            _today.Add((coin == "WIN", turn == "FIRST"));
            _lastCoin = coin;
            _lastTurn = turn;
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
                Front = _today.Count(t => t.front),
                First = _today.Count(t => t.first),
                LastCoin = _lastCoin,
                LastTurn = _lastTurn
            };
            s.Back = s.Total - s.Front;
            s.Second = s.Total - s.First;

            // 最近的连续同面
            int streak = 0;
            for (int i = _today.Count - 1; i >= 0; i--)
            {
                bool f = _today[i].front;
                if (streak == 0) streak = f ? 1 : -1;
                else if (f && streak > 0) streak++;
                else if (!f && streak < 0) streak--;
                else break;
            }
            s.Streak = streak;

            int take = Math.Min(8, _today.Count);
            for (int i = _today.Count - take; i < _today.Count; i++) s.Recent.Add(_today[i].front);

            s.Fortune = Fortune.For(s.Total, s.Front, s.Streak);
            return s;
        }
    }
}
