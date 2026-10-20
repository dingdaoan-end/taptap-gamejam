# Memu 主菜单

- 工程：`D:/TuanjieProjects/TapTapGameJamGreybox`。
- 已基于 GitHub 最新 main `953233f` 制作。
- 打开 `Assets/Scenes/Memu.unity`，点击 Play 即可使用。
- Canvas 使用 Scale With Screen Size，参考分辨率为 **1280×720**。
- `MemuCanvas/MenuComposition` 保持完整 16:9 构图，在其他屏幕比例下留边显示。
- 背景原图为 5334×3000，无需超分辨率；保留像素风 Point 采样，未重新生成背景。
- 两个按钮使用 imagegen 制作的透明像素文字，支持鼠标、上下方向键及确认键。
- 开始游戏异步打开 `Map`，加载期间防止重复点击。
- 退出游戏在正式游戏中关闭程序，在编辑器中停止 Play。
- Build Settings 首个启用场景为 `Memu`，第二个为 `Map`；保留原有 MusicGarden 条目。

## 在 Unity 中修改

- 背景：选择 `MemuCanvas/MenuComposition/Background` 的 Image。
- 按钮位置和大小：选择 `StartGameButton` 或 `QuitGameButton` 的 Rect Transform。
- 悬停、选中和按下颜色：修改按钮的 Button / Colors。
- 开始场景：选择 `MenuActions`，修改 Memu Controller 的 Gameplay Scene，并将目标场景加入 Build Settings。
- 按钮点击绑定已持久保存在场景中，可直接在 Button / On Click 中查看。
- 菜单背景音乐：`MenuMusic` 的 Audio Source，默认音量 0.45，2D 循环播放。进入 Map 后随菜单卸载停止，关卡使用自己的音乐。
- 音源文件：`Assets/Audio/Menu/LV1_Full Song.wav`，导入时使用 Vorbis 压缩与流式播放，原始 WAV 保留。
- 点击音效：`MenuClickSound`，约 0.22 秒的原创木质敲击与轻铃泛音。开始和退出都会先播完短音再执行，期间锁定两个按钮，避免连点。可在 Audio Source 调整音量或更换 Clip。

## 验证记录

- `Logs/memu-red.log`：创建前因缺失 Memu 场景失败。
- `Logs/memu-setup.log`：场景和启动列表生成成功。
- `Logs/memu-play.log`：实际 UI 点击命中开始按钮并加载 Map，菜单退出后无遗留 Canvas、无重复 AudioListener；退出按钮成功停止 Play。
- 1280×720 渲染截图：`Artifacts/Memu/menu-1280x720.png`。
- `Logs/memu-click-play.log`：开始和退出均先播放点击音效，再执行操作；按钮防连点及切换后的音频清理通过检查。
- Map 场景保持原样；菜单、素材、音频和脚本随本次版本提交。
