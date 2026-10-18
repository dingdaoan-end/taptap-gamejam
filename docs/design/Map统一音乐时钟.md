# Map 统一音乐时钟

Map 场景的 Castle 上挂有 `TapTapGameJam.Music.MusicClock`，绑定 Castle 原有 AudioSource。鼓文件保留不变。无需在每座塔上再放一个时钟，也不要让塔创建自己的音乐起始时间。

## Inspector

- BPM：当前 100；Lv.3/4 音频使用 116。
- Beats Per Bar：4。
- Bars Per Loop：8，即 32 拍。100 BPM 下为 19.2 秒，116 BPM 下约为 16.552 秒。
- Schedule Lead Seconds：0.2 秒，给音频准备留出时间。
- Drum Source：Castle 的 AudioSource。
- Play On Start：进入 Play 后加载鼓声，再预约启动。
- Pause On Focus Loss：失焦暂停，返回时恢复；不想在切换窗口或 Inspector 时暂停可关闭。

这些节奏参数在启动/Restart 时生效，运行中修改后按 Restart。塔范围样式、地图、建造菜单没有变更。

进入 Play，选中 Castle，可以在 Music Clock Inspector 底部看到绝对拍数、循环内拍数、循环秒数以及 Restart/Pause/Resume/Stop 按钮。拍号从 0 开始。第一次播放和恢复有约 0.2 秒预约等待，此时拍数保持不动。

## 鼓循环

当前 Map 与 MusicTest 使用 `Assets/Audio/Lv.1&2/Lv.1 Drums.wav`，长度 19.2 秒，对应 100 BPM 的 32 拍。2026-10-18 接入攻击时修复了 Map 对旧鼓声素材的失效引用，使用工程内已有的正确循环，不裁剪或变速。

更换音乐时，替换 Castle AudioSource 的 Clip 并 Restart，BPM 与循环拍数要对应素材。如果素材保留额外尾音，需要明确循环边界和重叠尾音调度，不能直接拿带尾音的文件长度作周期。

## 后续攻击脚本调用

给塔控制器保留一个序列化的 `MusicClock` 引用，场景中指向 Castle。运行时建造塔时，由建造控制器把同一个引用传给新塔。`MusicClock` 不依赖旧 PianoDefense 的固定 BPM。

```csharp
// 取得距离现在至少一个调度准备窗口的下一个半拍点。
double attackTime = musicClock.GetNextGridDspTime(2);

// 按已标注的变体时间表安排：第 loopIndex 个循环中的 beatOffset。
double absoluteBeat = loopIndex * musicClock.BeatsPerLoop + beatOffset;
double eventTime = musicClock.GetDspTimeForBeat(absoluteBeat);
```

调用预约接口前检查 `IsRunning && !IsPaused`；变体时间表可使用0、0.5、1.75等拍位置。`GetDspTimeForBeat`是绝对转换，不会替你跳过过去的事件，调度器应过滤过期项。`GetNextGridDspTime`跳过过去时点，不补发积压攻击。

`CurrentBeat`和`LoopBeat`由DSP时间原点直接计算，不按帧累加。掉帧后仍保持原相位；声音使用PlayScheduled，画面/战斗逻辑最早在到点后的游戏帧执行，因此视觉精度仍受帧率影响。

塔的音频/攻击调度器需要订阅 `TransportChanged`，或者记录 `Revision`。暂停、恢复、停止和重启都会变更Revision；收到变更时取消旧预约的声音和攻击，按新原点重新调度。不要保留恢复前的DSP时间戳。

暂停保存精确的音乐位置；恢复重新预约鼓声并保留音乐相位。当前鼓声按实际文件长度恢复采样位置，不会修正原文件自身的节奏问题。`Time.timeScale = 0`不等同于音乐暂停，暂停菜单应显式调用PausePlayback/ResumePlayback。

## 本次范围和验证

已接入：统一时钟、Castle鼓声预约启动、暂停/恢复/停止、实时Inspector、未来攻击的时间转换接口。

2026-10-18 已接入塔索敌、声部门控、可编辑拍点攻击及敌人伤害。使用方式见 `节拍攻击_使用说明.md`；本版不自动分析 WAV 音符起音。

计时测试位于 `Tests/MusicClockTests.cs`。本次使用临时.NET 10项目运行纯计时测试，并用Unity 2022.3.62f3编译器检查全部游戏运行时代码和编辑器代码。覆盖100/116 BPM、半拍网格、长时间卡顿、分数拍暂停/恢复、首拍前暂停和停止重置。

场景修改前备份及验证记录在 `Artifacts/music-clock-20261017/`（Git忽略）。未进行自动Play模式听感验证。
