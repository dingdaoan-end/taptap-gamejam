# 夜之乐章 · 钢琴塔防原型

主题：**涌现**。敌人携带音高，琴塔的击杀组成旋律。首版用于验证“布塔 → 按拍防守 → 击杀成曲 → 聆听回放”的循环。

## 运行钢琴塔防

- 编辑器：团结引擎 **1.10.4 / 2022.3.62t16**。
- 在这台电脑的团结 Hub 打开本项目后，编辑器会恢复 `Assets/Scenes/PianoDefense.unity`。点顶部的 Play 即可试玩；团队成员首次打开时可双击该场景，或使用菜单 **Tools → Game Jam → Open Piano Defense Prototype**。
- Windows 试玩：使用菜单 **Tools → Game Jam → Build Piano Defense for Windows** 构建，运行 `Builds/PianoDefense-v0.2/NocturnePiano.exe`。分享时打包整个 `PianoDefense-v0.2` 文件夹。
- 初始已有三座示例琴塔。按 **空格**开始守夜；也可点击“清空，自己布阵”。
- 点击深色空格建塔（8 币），点击琴塔选择“全部／独奏／跳过”和音高。`B` 切换建造，`Delete` 回收选中塔。
- `空格`或 `Esc` 暂停／继续。切出游戏自动暂停。准备和结算时可点击钢琴键试听。
- 结束后点击“聆听这一次战斗的乐章”，按原始节拍回放所有击杀音符。

## 本版规则

12×7 网格、三条固定通道；110 BPM；普通敌人每拍移动一格，**装饰音怪（»）每拍两格、只经过偶数列**，会赶在队头抢拍打乱击杀顺序；琴塔每拍单体攻击，曼哈顿射程为3，优先攻击离据点最近的合格目标。据点有10点生命。三波敌人加两次间奏约一分钟，末尾等剩余敌人退场。

目标旋律为 **do–mi–sol–do′**。每16拍结算；允许夹杂其他音高，但目标音必须依序出现在不同拍，相邻目标音间隔不超过4拍。同拍多杀算和声。**乐句祝福按层数结算**：每层+1币；2层以上据点回复1点（不超过上限）；完整4层触发**终止式**，清除场上四分之一敌人（向上取整），奖励击杀不记谱，防止连锁刷分。

开局24币、最多8塔、正常击杀+1币。准备阶段回收返还8币，开战后返还6币。结果显示得分、星级、完整旋律和击杀数量。

这是**按节拍结算的布塔游戏**，目前没有按键判定线。音乐来自防守的击杀顺序。钢琴采用带衰减泛音的合成占位音，后续可替换采样；音符由 `dspTime` 与 `PlayScheduled` 预排程。暂停会保留尚未呈现的拍，回放保留和声与停顿。

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
