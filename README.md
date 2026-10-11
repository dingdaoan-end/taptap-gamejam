# 夜之乐章 · 钢琴作曲塔防 v0.3

主题：**涌现**。敌人携带音高，琴塔的击杀组成旋律。首版用于验证“布塔 → 按拍防守 → 击杀成曲 → 聆听回放”的循环。

## 运行钢琴塔防

- 编辑器：团结引擎 **1.10.4 / 2022.3.62t16**。
- 在这台电脑的团结 Hub 打开本项目后，编辑器会恢复 `Assets/Scenes/PianoDefense.unity`。点顶部的 Play 即可试玩；团队成员首次打开时可双击该场景，或使用菜单 **Tools → Game Jam → Open Piano Defense Prototype**。
- Windows 试玩：使用菜单 **Tools → Game Jam → Build Composition v0.3 for Windows** 构建，运行 `Builds/PianoDefense-v0.3/NocturnePiano.exe`。分享时打包整个 `PianoDefense-v0.3` 文件夹。
- 四个乐章依次体验音符轨迹、交错节拍、三音共鸣和自由合奏。每关都有可直接演奏的示例布局；也可清空后自己布阵。
- 点击深色空格建塔（8 币），点击琴塔选择“全部／独奏／跳过”和音高。`B` 切换建造，`Delete` 回收选中塔。
- `空格`或 `Esc` 暂停／继续。切出游戏自动暂停。准备和结算时可点击钢琴键试听。
- 结束后点击“聆听这一次战斗的乐章”，按原始节拍回放所有击杀音符。

## 在团结编辑器中修改关卡

1. 打开 `Assets/Scenes/PianoDefense.unity`，在 Hierarchy 展开 **Piano Defense → Editable Board**。`Tiles` 下的 84 个格子和 `Starting Towers` 下的琴塔都是真正的场景对象，Scene 和 Game 显示的是同一套棋盘。
2. `Starting Towers` 内有四组乐章示例塔。移动、删除或复制对应组的琴塔；Transform 会吸附到格子。选中琴塔可在 Inspector 改音高、筛选、攻击节拍和所属乐章。查看其他组时可临时启用其对象。三条敌人通道仍是第 1、3、5 行。
3. 在 `Assets/PianoDefense/Lessons` 选中乐章资源，在 Inspector 修改标题、提示、BPM、开局金币、总拍数及敌人波次。若增加示例琴塔，请保证开局金币足够。
4. 在 Hierarchy 展开 **Composition UI**，可直接修改 Canvas 内的文字、按钮、面板、琴键和布局。编辑棋盘格子的 Material 或琴塔 Prefab 会改变实际游戏画面。
5. 按 **Ctrl+S** 保存场景，再点 Play 查看结果。Play 中临时布塔用于试玩；要保留关卡修改，请在非 Play 状态编辑场景。

界面是保存在场景里的 Canvas；棋盘、琴塔和音符敌人是实际场景对象。菜单 **Upgrade to Composition v0.3** 用于重建默认版式，会覆盖当前布局；通常只需直接编辑现有场景。

## 本版规则

12×7 网格、三条固定通道；四关分别为 100、108、104、112 BPM。普通敌人每拍移动一格，**装饰音怪（»）每拍两格**，会抢拍改变击杀顺序。琴塔在指定攻击拍单体攻击，曼哈顿射程为3，优先攻击离据点最近的合格目标。据点有10点生命，末尾等剩余敌人退场。

目标旋律为 **do–mi–sol–do′**。每16拍结算；允许夹杂其他音高，但目标音必须依序出现在不同拍，相邻目标音间隔不超过4拍。同拍多杀算和声。**乐句祝福按层数结算**：每层+1币；2层以上据点回复1点（不超过上限）；完整4层触发**终止式**，清除场上四分之一敌人（向上取整），奖励击杀不记谱，防止连锁刷分。

开局24币（第三关32币）、最多8塔、正常击杀+1币。准备阶段回收返还8币，开战后返还6币。结果显示得分、生命、音符和共鸣数量。

琴塔可选择每拍、正拍（1/3）、反拍（2/4）或首拍（1）攻击，使用统一的全局四拍相位。同一拍击杀 Do、Mi、Sol 触发共鸣：额外获得 3 币，场上存活敌人停走下一拍。同音重复不会代替缺少的和弦音。

这是**按节拍结算的布塔游戏**，目前没有按键判定线。音乐来自防守的击杀顺序。钢琴使用 Salamander Grand Piano 真实采样；音符由 `dspTime` 与 `PlayScheduled` 预排程。暂停会保留尚未呈现的拍，回放保留和声与停顿。美术、字体、音频的来源及授权见 [第三方资源说明](THIRD_PARTY_NOTICES.md)。

本版聚焦一夜守卫；白天采风、多乐器、其他敌人、祝福装配、Boss 仍属于后续内容。原始设计见 [系统设计文档](docs/design/音游塔防_系统设计文档_v1.md)。

## 程序结构与验证

| 文件 | 职责 |
| --- | --- |
| `Assets/PianoDefense/Runtime/DefenseSimulation.cs` | 波次、移动、布塔、筛选、击杀、奖励和结算 |
| `Assets/PianoDefense/Runtime/MelodyMatcher.cs` | 乐句顺序、同拍限制、最大间隔 |
| `Assets/PianoDefense/Runtime/BeatClock.cs` | 提前调度、暂停、卡顿后的时钟恢复 |
| `Assets/PianoDefense/Runtime/PianoAudio.cs`、`PianoSynthesis.cs` | 采样复音播放与无资源时的合成后备 |
| `Assets/PianoDefense/Runtime/PianoDefenseGame.cs` | 战斗生命周期与音画衔接、回放 |
| `Assets/PianoDefense/Runtime/PianoCompositionUI.cs`、`CompositionBoardInput.cs` | Canvas 界面与棋盘交互 |
| `Assets/PianoDefense/Runtime/CompositionLessonAsset.cs`、`CompositionLessons.cs` | 可编辑关卡资源与默认教学配置 |
| `Assets/PianoDefense/Editor/PianoCompositionBuilder.cs` | v0.3 场景生成、检查与 Windows 构建 |

核心测试不依赖编辑器和第三方测试包，使用 .NET 8：

```powershell
dotnet run --project Tests/PianoDefense.CoreTests
```

覆盖音高筛选、漏怪、重复击杀、建造成本、回收、乐句边界、和声限制、终止式、胜负、确定性音符日志、暂停续播、节拍相位、三音共鸣以及四关示例通关。Windows 构建使用菜单 **Tools → Game Jam → Build Composition v0.3 for Windows**。

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

## 夜之乐章可玩版 v0.4（2026-10-11）

新版场景：Assets/Scenes/MusicGarden.unity。Windows运行包：Builds/MusicGarden-v0.4/Nocturne.exe。使用说明：docs/design/夜之乐章_可玩版v0.4_使用说明.md。

四区精确地图、普通/和弦琴塔、三类怪物、自动伴奏与A/S/D领奏、合奏休止、间奏整备、失败重试和真实演奏回放已接入。默认构建场景为MusicGarden，旧PianoDefense教学场景保留供参考。

镜头先完整展示四区约3秒，再移到左上区，精确框住25×13格；宽屏和4:3窗口均不露出邻区。整备或间奏时，可在任意空闲草地建造；石台只是推荐位置，道路、障碍、基地和已占用格不可建造。

拉取工程后，用团结1.10.4打开项目并进入 `Assets/Scenes/MusicGarden.unity`。Windows构建菜单为 **Tools → Music Garden → Build Windows**，运行包输出到 `Builds/MusicGarden-v0.4/`。构建缓存与本地可执行文件不纳入源码提交。

新版核心测试：`dotnet run --project Tests/MusicGarden.CoreTests`。
