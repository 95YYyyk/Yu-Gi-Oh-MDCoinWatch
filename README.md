# Yu-Gi-Oh MDCoinWatch

大师决斗的投币运势浮窗。后台看屏幕，记录每局的**投币胜负**和**实际先后手**，再给一句今日运势。

纯读屏幕像素：不注入、不改内存、不读游戏进程、不联网。

![浮窗](docs/widget.png)

**A tiny always-on-top overlay for Yu-Gi-Oh! Master Duel** — records, duel by duel, whether you won the coin toss and who actually goes first, plus a daily luck line. Screen-reading only. Chinese UI.

## 下载

[**Releases →**](https://github.com/95YYyyk/Yu-Gi-Oh-MDCoinWatch/releases/latest)

| 文件 | 大小 | 说明 |
| --- | --- | --- |
| `YuGiOh-MDCoinWatch.exe` | 11 MB | 自带运行时，双击就跑 |
| `YuGiOh-MDCoinWatch-lite.exe` | 0.8 MB | 需要 .NET 8 运行时 |

## 用法

1. 游戏用 **16:9** 分辨率、**窗口或无边框窗口**运行
2. 双击 exe，浮窗出现在屏幕右上角
3. 切回游戏正常打，浮窗实时更新

**右键浮窗**：文字颜色、强调色、不透明度、大小、锁定位置、退出
**拖动**：拉边缘缩放，拉中间移动

## 浮窗显示什么

| 位置 | 含义 |
| --- | --- |
| 左上 金币 + 次数 + 百分比 | 今天投币猜赢的次数和比例 |
| 右上 黑币 + 次数 + 百分比 | 反面（被对手选走）的次数和比例 |
| 先手 / 后手 | 今天先攻、后攻的局数 |
| 今日运势 | 按正面率分档的文案，连正 / 连负有彩蛋 |
| 右上小圆点 | 绿 = 已锁定游戏，黄 = 对局中，灰 = 等游戏 |

## 注意事项

- 别用**独占全屏**，抓屏会拿到黑帧。窗口 / 无边框窗口没问题
- 分辨率用 **16:9**（1600x900 / 1920x1080 / 2560x1440 都行）
- 别把浮窗拖到屏幕**中下部那条横带**上，那是识别区，压住会暂停并提示
- 只在**游戏处于前台**时识别，切出去看数据会自动暂停
- 点浮窗、拖它、缩放它**都不抢游戏焦点**（右键里有「锁定位置与大小」防误碰）
- exe **没有代码签名**，首次运行 SmartScreen 会拦：点「更多信息」→「仍要运行」
- **不联网**、不写注册表；CSV 不记对手、不记卡组、不记你的账号
- 只统计**今天**的记录，历史都在 CSV 里
- 日志里反复出现「没捕捉到横幅」，就把 ini 里的 `fps` 从 10 提到 20

## 数据

exe 旁边会生成三个文件，**建议单独放一个文件夹**：

- `duel_stats.csv` —— 每局一行
- `YuGiOh-MDCoinWatch.ini` —— 颜色、位置、大小、进程名、fps
- `YuGiOh-MDCoinWatch.log` —— 出问题看这个

```csv
time,coin,turn_order,duel_seconds,note
2026-10-02 21:10:00,WIN,FIRST,143,结束画面出现
```

## 原理

灰度模板匹配（TM_CCOEFF_NORMED），**先定屏再判内容**：投币以「先攻/后攻按钮在不在」为主判据，文字只做二次确认。窗口尺寸按 1920x1080 等比换算，三种分辨率实测命中 ≥ 0.968、误报 ≤ 0.366。

## 开发

```
app/       C# / .NET 8，界面是纯 Win32 + GDI 自绘（无 WinForms，所以自带运行时能裁到 11 MB）
tools/     模板生成、硬币抠图、一键发布
probe/     可行性分析脚本
build.ps1  构建，-Both 出两个版本
```

```powershell
.\build.ps1 -Both
.\dist\YuGiOh-MDCoinWatch.exe --selftest=你的截图目录   # 离线核对识别分数
```

仓库**不含游戏截图**（涉及玩家 ID），要跑 `--selftest` 得自备截图。游戏界面截图放在 [docs/screenshots](docs/screenshots)。

## 声明

非官方工具，仅读取屏幕像素，不修改游戏、不模拟输入。游戏素材版权归 KONAMI 所有。MIT License。
