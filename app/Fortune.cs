namespace MdCoinWatch;

/// <summary>
/// 今日运势文案。优先级：彩蛋 -> 连正/连反 -> 没数据 -> 硬币 × 胜率 5x3 矩阵 -> 单维兜底。
/// 文案池在 运势文案候选.md，改完文案跑 tools/gen_fortune.py 重新生成本文件。
/// </summary>
internal static class Fortune
{
    // 0.1 硬币连反 ≥5，却一局没输
    private static readonly string[] E01 =
    {
        "我命由我不由天",
        "天要我后手，我偏要赢",
        "硬币不给的，我自己拿",
        "这就是羁绊的力量",
        "后手全胜，你有点东西",
    };

    // 0.2 硬币连正 ≥10
    private static readonly string[] E02 =
    {
        "这就是鸿运齐天蛊的感觉吗",
        "硬币：今天只认你一个",
        "欧气已经溢出屏幕了",
        "先攻拿到封神了",
        "你确定没改硬币的代码？",
    };

    // 0.3 硬币连正 ≥5，却一局没赢
    private static readonly string[] E03 =
    {
        "欧成这样还能全输，服了",
        "硬币都白给你了",
        "先攻全给了，你全输了",
        "运气和你是两条平行线",
    };

    // 0.4 今天全胜
    private static readonly string[] E04 =
    {
        "今天没输过，别停",
        "对手的 LP 都归零了吧",
        "这状态留着上分",
        "一路通杀，收手吧",
    };

    // 0.5 今天全败
    private static readonly string[] E05 =
    {
        "LP 归零，收工吧",
        "一局没赢，明天再来",
        "这战绩，先睡一觉吧",
        "你已经没什么可以失去了",
    };

    // 0.6 硬币一次反面都没有
    private static readonly string[] E06 =
    {
        "一次反面都没有，你确定？",
        "这硬币是不是坏了",
        "硬币已经被你收服了",
    };

    // 0.7 硬币一次正面都没有
    private static readonly string[] E07 =
    {
        "一次正面都没有，离谱",
        "你和硬币是真的有仇",
        "硬币：后手，永远的后手",
    };

    // 0.8 正反刚好各一半
    private static readonly string[] E08 =
    {
        "天道均衡，谁也不欠",
        "五五开得刚刚好",
        "硬币和你达成了和解",
    };

    // 0.9 五连胜
    private static readonly string[] E09 =
    {
        "五连胜，别停",
        "手热成这样，再来一把",
        "这波连击，别断",
    };

    // 0.10 五连败
    private static readonly string[] E10 =
    {
        "五连败，换套卡组吧",
        "手冷了，歇会儿吧",
        "断一下这个连败",
    };

    // 0.11 深夜局
    private static readonly string[] E11 =
    {
        "凌晨还在打牌，注意身体",
        "这个点还决斗，也是真爱",
        "深夜场，牌桌上见",
    };

    // 0.12 局数里程碑
    private static readonly string[] E12 =
    {
        "N 局了，歇会儿吧",
        "N 局，你是真要上分啊",
        "又打了 N 局，今天很拼",
    };

    // 0.13 开门红
    private static readonly string[] E13 =
    {
        "开门红，今天有戏",
        "第一局就赢，好兆头",
    };

    // 1. 连正面 ≥ 5
    private static readonly string[] K_P5 =
    {
        "N 连正面，菜是菜，运气是真行",
        "N 连正面，抽卡也这么准就好了",
        "N 连正面，先攻拿多了也不会赢",
        "N 连正面，你这是充钱了吧",
        "N 连正面，对面已经摔牌了",
        "N 连正面，你这硬币是焊死的吧",
        "N 连正面，先攻权被你承包了",
        "N 连正面，趁欧气还在快去神抽",
    };

    // 1. 连反面 ≥ 5
    private static readonly string[] K_N5 =
    {
        "N 连反面，菜就多练后手",
        "N 连反面，运气差还打后手",
        "N 连反面，你和先攻八字不合",
        "N 连反面，习惯当后手了吧",
        "N 连反面，硬币都嫌你手气",
        "N 连反面，你和硬币是不是有仇",
        "N 连反面，这硬币绝对有毒",
        "N 连反面，换枚硬币谈谈吧",
    };

    // 1. 连正面 ≥ 3
    private static readonly string[] K_P3 =
    {
        "N 连正面，趁没卡手赶紧打",
        "N 连正面，别浪费这波手气",
        "N 连正面，再赢你可就飘了",
        "N 连正面，手感来了别停",
        "N 连正面，今天先攻管够",
        "N 连正面，硬币站你这边",
    };

    // 1. 连反面 ≥ 3
    private static readonly string[] K_N3 =
    {
        "N 连反面，洗脸也不管用了",
        "N 连反面，后手打着打着就惯了",
        "N 连反面，去洗个手吧兄弟",
        "N 连反面，先去洗把脸",
        "N 连反面，后手也是一种修行",
        "N 连反面，今天走防守反击",
    };

    // 2. 今天还没开张（0 局）
    private static readonly string[] D0 =
    {
        "今天还没开张，是怕输吗",
        "一局没打，战绩还是完美的",
        "还没开打，慌也没用",
        "今天还没开张，硬币在等你",
        "今天还没开张，先热个手",
    };

    // 2. 样本太少（1~2 局）
    private static readonly string[] D1 =
    {
        "才几局，先别急着怪硬币",
        "再打几局，别找借口",
        "样本太少，多打几局再看",
        "运势还在加载中",
    };

    // 3. 硬币 ≥70% ｜ 胜率 ≥60%
    private static readonly string[] M55 =
    {
        "手气好又会打，你开挂了吧",
        "运气技术双开，还让不让人玩",
        "欧还强，对面直接投降吧",
        "这局赢面都写脸上了",
        "天选之子，先攻胜率都拿满",
        "今天适合冲分，别浪费时间",
    };

    // 3. 硬币 ≥70% ｜ 胜率 40~60%
    private static readonly string[] M54 =
    {
        "手气不错，可惜技术一般",
        "硬币都帮你，你也就打成这样",
        "先攻拿到手软，胜率却平平",
        "运气喂到嘴边，你嚼不动",
        "运气在线，技术再加把劲",
        "先攻占便宜了，别浪费",
    };

    // 3. 硬币 ≥70% ｜ 胜率 <40%
    private static readonly string[] M53 =
    {
        "手气这么好还能输，绝了",
        "硬币都替你铺路了还输",
        "给你先攻你都打不赢",
        "这手气配这胜率，暴殄天物",
        "先攻全给你，还是输麻了",
        "运气没问题，牌再练练",
        "先攻拿得多，胜率得跟上",
    };

    // 3. 硬币 58~70% ｜ 胜率 ≥60%
    private static readonly string[] M45 =
    {
        "先攻占便宜，你也真会用",
        "手气偏好，赢得一点不含糊",
        "顺风不浪，这局打得漂亮",
        "运气给力，技术更给力",
        "硬币站你这边，牌也没掉链子",
    };

    // 3. 硬币 58~70% ｜ 胜率 40~60%
    private static readonly string[] M44 =
    {
        "先攻拿得多，胜率没跟上",
        "手气给你台阶，你没踩稳",
        "运气还行，打得一般",
        "优势在手，别打散了",
        "顺风顺水，稳着来",
    };

    // 3. 硬币 58~70% ｜ 胜率 <40%
    private static readonly string[] M43 =
    {
        "硬币帮你开路，你自己走偏了",
        "先攻占便宜，还输这么多",
        "运气给了，你就这么用？",
        "手气不差，牌打得稀烂",
        "别再怪运气了，真不是它的问题",
        "手气还行，牌再理理",
        "先攻多，别急着乱出",
    };

    // 3. 硬币 45~58% ｜ 胜率 ≥60%
    private static readonly string[] M35 =
    {
        "硬币不帮你，你偏要赢",
        "没靠运气，纯纯技术压制",
        "手气一般还能赢，有点东西",
        "运势平平，胜率倒是很硬",
        "不靠硬币，靠牌技",
        "五五开也能赢，稳",
    };

    // 3. 硬币 45~58% ｜ 胜率 40~60%
    private static readonly string[] M34 =
    {
        "手气普通，赢得也费劲",
        "五五开，硬碰硬",
        "手气一般，赢面不一般",
        "硬实力，没什么好说的",
        "中规中矩，能赢就行",
    };

    // 3. 硬币 45~58% ｜ 胜率 <40%
    private static readonly string[] M33 =
    {
        "手气一般，打得也一般",
        "什么都一般，图个乐吧",
        "五五开，菜鸡互啄",
        "没运气可赖了，得练",
        "运势平平，胜率平平",
        "中规中矩，还有进步空间",
    };

    // 3. 硬币 32~45% ｜ 胜率 ≥60%
    private static readonly string[] M25 =
    {
        "后手打成这样，你有点东西",
        "硬币坑你，你自己赢回来了",
        "天崩开局硬是打成这样",
        "先攻没拿到，胜率照样高",
        "逆风也能赢，稳得可怕",
        "运气不给力，实力顶上",
    };

    // 3. 硬币 32~45% ｜ 胜率 40~60%
    private static readonly string[] M24 =
    {
        "运气差，胜率倒是没崩",
        "硬币坑你，你还撑住了",
        "手气不佳，稳住了阵脚",
        "后手多，赢得也不少",
        "硬币欠你的，牌技还回来了",
    };

    // 3. 硬币 32~45% ｜ 胜率 <40%
    private static readonly string[] M23 =
    {
        "运气差，牌也没打明白",
        "硬币不帮你，你自己也悬",
        "今天逆风，稳一点",
        "背就背了，别输得难看",
    };

    // 3. 硬币 <32% ｜ 胜率 ≥60%
    private static readonly string[] M15 =
    {
        "硬币全程不看你，你全赢了",
        "后手打满还赢麻了，离谱",
        "硬币：我拦不住他",
        "这是把运气换成了实力",
        "开局就落后，硬是追回来",
        "逆风局专精",
    };

    // 3. 硬币 <32% ｜ 胜率 40~60%
    private static readonly string[] M14 =
    {
        "硬币一直坑你，你还站得住",
        "后手打多了，也练出来了",
        "背归背，没崩盘",
        "全靠后手硬撑",
    };

    // 3. 硬币 <32% ｜ 胜率 <40%
    private static readonly string[] M13 =
    {
        "硬币和你杠上了，认命吧",
        "运气差，牌也没跟上",
        "今天先别上分了",
        "背成这样，能撑住就不错",
        "先歇了，明天再战",
    };

    // 4. 偏旺（正面率 ≥ 70%）
    private static readonly string[] F5 =
    {
        "欧成这样，输了就丢人了",
        "运气这么好，牌技跟上了吗",
        "硬币都帮你，你还能输",
        "欧皇附体，硬币都听你的",
        "这手气，抽卡包都能出罕贵",
    };

    // 4. 略旺（58% ~ 70%）
    private static readonly string[] F4 =
    {
        "手气偏旺，可惜打得一般",
        "硬币赏脸，别打崩了",
        "运势偏旺，先攻手牌也顺",
        "手气不错，适合打先攻卡组",
    };

    // 4. 平平（45% ~ 58%）
    private static readonly string[] F3 =
    {
        "五五开，那就看谁更菜了",
        "没运气可赖了，看技术",
        "运势平平，全靠牌技",
        "五五开，谁先攻看本事",
    };

    // 4. 略背（32% ~ 45%）
    private static readonly string[] F2 =
    {
        "有点背，但主要还是菜",
        "硬币不帮你，自己看着办",
        "背归背，牌总该会打吧",
        "有点背，但还能打",
        "手气偏凉，留点后手",
    };

    // 4. 很背（< 32%）
    private static readonly string[] F1 =
    {
        "别打先攻了，你没那个命",
        "硬币：后手就是你的归宿",
        "这运气，打人机都费劲",
        "输了别赖硬币，它尽力了",
        "今天和硬币八字不合",
        "认命吧，后手有后手的打法",
    };

    // 硬币 × 胜率：M[硬币档][胜率档]
    private static readonly string[][][] M =
    {
        new[] { M55, M54, M53 },
        new[] { M45, M44, M43 },
        new[] { M35, M34, M33 },
        new[] { M25, M24, M23 },
        new[] { M15, M14, M13 },
    };

    // 单维兜底：F[硬币档]
    private static readonly string[][] F =
    {
        F5, F4, F3, F2, F1,
    };

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
