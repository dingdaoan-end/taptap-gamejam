# 夜之乐章 · 钢琴塔防原型

主题：**涌现**。敌人携带音高，琴塔的每次命中组成和弦。首版用于验证“布塔 → 按拍防守 → 击杀成曲 → 聆听回放”的循环。

## 运行钢琴塔防

- 编辑器：Unity **2022.3.62f3**。
- 在 Unity Hub 打开本项目后，编辑器会恢复 `Assets/Scenes/PianoDefense.unity`。点顶部的 Play 即可试玩；团队成员首次打开时可双击该场景，或使用菜单 **Tools → Game Jam → Open Piano Defense Prototype**。
- Windows 试玩：使用菜单 **Tools → Game Jam → Build Piano Defense for Windows** 构建，运行 `Builds/PianoDefense-v0.2/NocturnePiano.exe`。分享时打包整个 `PianoDefense-v0.2` 文件夹。
- 初始已有四座示例琴塔。按 **空格**开始守夜；也可点击“清空，自己布阵”。
- 点击深色空格建塔（8 币），点击琴塔选择“全部／独奏／跳过”和音高。`B` 切换建造，`Delete` 回收选中塔。
- `空格`或 `Esc` 暂停／继续。切出游戏自动暂停。准备和结算时可点击钢琴键试听。
- 结束后点击“聆听这一次战斗的乐章”，按原始节拍回放所有命中音符。

## 示例关卡切换与配置

左侧菜单、暂停菜单和结算面板提供 **4536251** / **卡农简化版** 切换按钮。切换会停止当前音频与回放，清空本局战斗记录和待执行操作，恢复所选关卡的四塔示例布局。清空布阵、恢复示例、重新开局均保留当前关卡选择；启动默认进入卡农。

| 关卡 | 乐谱 | 敌人数 | 保存的出怪配置 |
| --- | --- | --- | --- |
| 4536251 | F–G–Em–Am–Dm–G–C，16 拍 | 37 | [progression-4536251.json](Assets/PianoDefense/Resources/Levels/progression-4536251.json) |
| 卡农简化版 | C–G–Am–Em–F–C–F–G 两轮＋C 收尾，36 拍 | 87 | [canon.json](Assets/PianoDefense/Resources/Levels/canon.json) |

4536251 恢复此前的原创主旋律：A5 C6 / B5 G5 / G5 B5 / A5 C6 / F5 A5 / B5 G5 / E5 D5 C5 C5。两关均为 55 BPM、主旋律一击、伴奏两击，最后 C 伴奏四击；保留当前下方横排三格攻击范围，结尾仍需要补刀。

JSON 为实际运行时读取的数据源，构建时随 Resources 打包。每条出怪记录保存从零开始的 `beat`、网格行 `lane`（1/3/5/7）、音名 `pitch`、`speed` 和 `hits`；另存 `totalBeats`、`initialCoins`、示例 `towers` 和和弦时间表 `chords`。改配置后重新载入关卡即可生效，非法音名、布局或超出乐谱的出怪会被校验拒绝。

## 本版规则

12×9 网格、四条声部通道，55 BPM。当前默认曲目为帕赫贝尔《卡农》的 **C 大调简化版**：第一行演奏开头两段主题，下方三行弹奏 **C–G–Am–Em–F–C–F–G（15634145）**，循环两轮，最后增加四拍 C 和弦收尾。

| 段落 | 第一行主题（每音占两拍、按拍重奏） |
| --- | --- |
| 第一轮，1–16 拍 | E6、D6、C6、B5、A5、G5、A5、B5 |
| 第二轮，17–32 拍 | C6、B5、A5、G5、F5、E5、F5、D5 |
| 收尾，33–36 拍 | C5，每拍敲击一次，共四次 |

这是主题加三和弦的简化改编，未加入原曲的多声部轮奏与快速变奏。主题移调到 C 并上移八度，保证第一行音高高于伴奏。G 和弦使用 D4–G4–B4 转位，便于维持声部顺序。沿用现有合成钢琴，不添加新采样。

旋律敌人每拍出现一个，命中一次消失；伴奏敌人每组需命中两次，结尾 C 需四次。四座示例塔分别负责主旋律及三个伴奏声部。

旋律与和声核对来源：[Canon in D 乐谱与音符表](https://piano.org/songs/canon-in-d/)。改编保留开头主题的音高顺序，将长音改为重复敲击以适配当前攻击机制。

每座塔每拍攻击一次，每个敌人每拍最多被命中一次；**每次命中都发声并记录回放**，仅最后一次命中奖励击杀与金币。四座示例塔各负责一条声部，乐谱编排为 36 拍（约 39.3 秒）、87 个敌人、144 次潜在发声。当前下方横排三格范围可让普通速度敌人连续受击三拍；结尾四击敌人仍需要下游琴塔补刀，四座示例塔不能保证完整演奏。自由修改布阵与筛选会改变实际演奏和漏怪结果。

初始32币，建塔8币、最多8塔，攻击范围为正下方一格及其左右相邻两格（下方横排三格）；准备阶段回收8币，开战后回收6币。敌人每拍移动一格，据点10点生命。此次和弦编排停用旧的旋律祝福和终止式清怪，避免提前截断和弦；旧规则仍保留在自定义波次测试中。三颗星分别对应击杀率至少90%、据点至少8点生命、至少36拍和声。

音符由 `dspTime` 与 `PlayScheduled` 预排程；暂停保留未呈现的拍，回放保留全部命中、和声与停顿。本段演奏音域为 C4–E6，已补齐主旋律所需高音，采用合成钢琴占位音。

本版聚焦一夜守卫；白天采风、多乐器、其他敌人、祝福装配、Boss 仍属于后续内容。原始设计见 [系统设计文档](docs/design/音游塔防_系统设计文档_v1.md)。

## 程序结构与验证

| 文件 | 职责 |
| --- | --- |
| `Assets/PianoDefense/Runtime/DefenseSimulation.cs` | 波次、移动、布塔、筛选、击杀、奖励和结算 |
| `Assets/PianoDefense/Runtime/MelodyMatcher.cs` | 乐句顺序、同拍限制、最大间隔 |
| `Assets/PianoDefense/Runtime/BeatClock.cs` | 提前调度、暂停、卡顿后的时钟恢复 |
| `Assets/PianoDefense/Runtime/PianoAudio.cs`、`PianoSynthesis.cs` | 合成钢琴与复音播放 |
| `Assets/PianoDefense/Runtime/PianoDefenseGame.cs` | 战斗生命周期与音画衔接、回放 |
| `Assets/PianoDefense/Runtime/PianoDefenseView.cs` | 可缩放的中文原型界面 |
| `Assets/PianoDefense/Editor/PianoDefenseBuilder.cs` | 场景生成与 Windows 构建菜单 |

核心测试不依赖编辑器和第三方测试包，使用 .NET 8：

```powershell
dotnet run --project Tests/PianoDefense.CoreTests
```

覆盖音高筛选、漏怪、重复击杀、建造成本、回收、乐句边界、和声限制、终止式、胜负、确定性音符日志、暂停续播和卡顿恢复。Windows 构建使用菜单 **Tools → Game Jam → Build Piano Defense for Windows**。

## 团队用 GitHub 的做法

GitHub 适合这个规模的 Game Jam。提交 `Assets/`、`Packages/`、`ProjectSettings/` 和根目录的 `.gitignore`；**不要提交** `Library/`、`Temp/`、`Obj/`。资源文件和对应 `.meta` 文件必须一起提交，因为 Unity 用 `.meta` 中的 ID 保存引用。本项目的 Asset Serialization 已设为 **Force Text**。

建议的最小流程：

1. 在 Discord / 飞书中固定一份简短任务板，标记负责人、文件范围、完成标准；每天开工和收工各同步一次可玩的状态。
2. `main` 始终保持可运行。每个人从 `main` 拉短分支，例如 `feature/player`、`art/tiles`；完成一个小任务就提交并提 PR，另一人检查是否能打开场景和玩一轮后再合并。
3. 明确场景所有权：一人负责主场景整合，其他人尽量做 Prefab、独立场景、脚本或美术资源。多人同时改同一 `.unity` 或 Prefab 容易产生难处理的合并冲突。
4. 美术先约定像素尺寸、PPU、Sorting Layer、命名和导入方式；程序先约定角色尺寸、速度、碰撞层、接口。尽早用占位资源跑通整条流程。
5. 每次合并前拉取最新 `main`，在本机打开 Unity 看 Console，试玩受影响的场景。收工时由一人做可运行构建，并保留一个稳定构建。

如果出现大量 PSD、音频源文件或大贴图，再给这些**二进制源文件**配置 Git LFS，并确保每位成员安装 Git LFS。当前原型不需要 LFS。不要一开始把全队工作压在一个场景文件上。

## 建议的职责边界

- 主程序：项目版本、输入和角色接口、场景整合、构建、合并检查。
- 玩法程序：独立系统和 Prefab，先约定对外接口再开发。
- 策划：一页核心循环、胜负条件、关卡节奏；主题公布后尽快砍到一个能完成的点子。
- 美术 / 音频：统一资产规格，先交一套能替换占位资源的最小资源。

主题公布后的第一轮讨论可以只回答四个问题：玩家做什么、独特机制是什么、三分钟试玩能体验到什么、哪些内容可以砍掉。优先在第一天做出可玩的完整循环，再加内容与表现。
