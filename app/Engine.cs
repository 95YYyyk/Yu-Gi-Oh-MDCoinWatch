namespace MdCoinWatch;

/// <summary>
/// 一局的状态机：空闲 -> 判定投币 -> 等先后手横幅 -> 对局中 -> 开局落一条记录。
/// 只吃检测结果，不碰屏幕，所以离线回放和实时跑的完全是同一条逻辑。
///
/// 记录时机：看到先后手横幅（或等不到横幅超时）就写 CSV 并更新界面统计；
/// 看到结束画面只是把同一条记录补上时长。所以浮窗在开局那一刻就会动。
/// </summary>
internal sealed class Engine
{
    private readonly Options _o;
    private readonly Recorder _rec;
    private readonly Action<string> _log;

    private DateTime _coinAt, _turnAt;
    private long _rowOffset = -1;
    private DateTime _rowTime;

    internal int State { get; private set; }          // 0 空闲 / 1 已判投币 / 2 对局中
    internal Coin Coin { get; private set; }
    internal Turn Turn { get; private set; }
    internal int Records { get; private set; }

    /// <summary>开局落记录时回调，界面靠它更新统计。</summary>
    internal Action<Coin, Turn>? OnRecorded;

    internal Engine(Options o, Recorder rec, Action<string> log)
    {
        _o = o; _rec = rec; _log = log;
    }

    internal void OnCoin(Coin c, DateTime now)
    {
        if (c == Coin.None) return;
        if (State != 0 && c == Coin) return;                 // 同一屏持续存在，不算新事件

        // 上一局还没看到结束画面就又开了一局：把上一条补个说明，不补时长
        if (State == 2) CloseRow("未见结束画面", now, false);
        else if (State == 1) { _log("[投币] 上局没走完就出现了新的投币界面"); }

        Coin = c; Turn = Turn.None; _coinAt = now; State = 1;
        _log("[投币] " + Core.CoinText(Coin) + (c == Coin.Win ? "   (出现了先攻/后攻选择)" : "   (被对手选走)"));
    }

    internal void OnTurn(Turn t, DateTime now)
    {
        if (State != 1 || t == Turn.None) return;
        Turn = t; _turnAt = now; State = 2;
        _log("[先后手] " + Core.TurnText(Turn) + "   (本局投币 " + Core.CoinText(Coin) + ")");
        OpenRow(now);
    }

    internal bool OnEnd(DateTime now)
    {
        if (State != 2) return false;
        CloseRow("结束画面出现", now, true);
        return true;
    }

    /// <summary>时间相关的兜底：横幅没捕捉到、对局卡住。</summary>
    internal void Tick(DateTime now)
    {
        if (State == 1 && (now - _coinAt).TotalSeconds > 25)
        {
            Turn = Turn.None; _turnAt = now; State = 2;
            _log("[先后手] 没捕捉到横幅，本局先后手记空，继续按对局处理。");
            OpenRow(now);
        }
        else if (State == 2 && (now - _turnAt).TotalMinutes > 45)
        {
            CloseRow("超时未见结束画面", now, false);
        }
    }

    /// <summary>开局：先落一条记录，界面统计立刻更新。</summary>
    private void OpenRow(DateTime now)
    {
        _rowTime = now;
        _rowOffset = _rec.Append(now, Core.CoinText(Coin), Core.TurnText(Turn), null, "对局开始");
        Records++;
        _log("[记录] 投币 " + Core.CoinText(Coin) + " / 先后手 " + Core.TurnText(Turn));
        OnRecorded?.Invoke(Coin, Turn);
    }

    /// <summary>收尾：把同一条记录补上时长和说明。</summary>
    private void CloseRow(string why, DateTime now, bool withDuration)
    {
        if (_rowOffset >= 0)
        {
            double? secs = withDuration && _turnAt != default ? (now - _turnAt).TotalSeconds : null;
            _rec.Rewrite(_rowOffset, _rowTime, Core.CoinText(Coin), Core.TurnText(Turn), secs, why);
            _log("[更新] 投币 " + Core.CoinText(Coin) + " / 先后手 " + Core.TurnText(Turn)
                 + " / 时长 " + (secs.HasValue ? (int)secs.Value + "s" : "-") + "   (" + why + ")");
        }
        _rowOffset = -1;
        Coin = Coin.None; Turn = Turn.None; State = 0;
    }
}
