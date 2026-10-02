namespace MdCoinWatch;

/// <summary>今日运势文案：按今日硬币正面率分档，连正/连负优先触发彩蛋。</summary>
internal static class Fortune
{
    internal static string For(int total, int front, int streak)
    {
        if (streak >= 5) return streak + " 连正面，你今天是不是开挂了";
        if (streak <= -5) return (-streak) + " 连反面，这硬币绝对有毒吧";
        if (streak >= 3) return streak + " 连正面，趁手热再来一把";
        if (streak <= -3) return (-streak) + " 连反面，要不先去洗把脸";

        if (total == 0) return "今天还没开张，先来一局";
        if (total < 3) return "样本还少，多打几局再看";

        double p = (double)front / total;
        if (p >= 0.70) return "欧皇附体，硬币都听你的";
        if (p >= 0.58) return "运势偏旺，先攻手牌也顺";
        if (p >= 0.45) return "运势平平，全靠牌技";
        if (p >= 0.32) return "有点背，但还能打";
        return "今天和硬币八字不合";
    }
}
