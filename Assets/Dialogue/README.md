# 对话占位资源与 UI 流程

目前接入 Map 的流程是：第一关开场对话 → 建造准备 → 点击“开始战斗” → 原有第一关测试入口开始刷怪。

## 修改台词

在 Project 中选择 `Assets/Dialogue/L1_Intro.asset`，在 Inspector 的 `Lines` 列表中修改每句 `Text`。每句可引用 `Guide.asset`，也可通过 `Portrait Override` 覆盖立绘。

- `L1_Intro`：第一关开场，两句占位对白。
- `L1_Clear`～`L4_Clear`：四关通关对白占位。
- `Common_Fail`：共用失败对白占位。
- `Level1`～`Level4`：关卡对话引用。第二至第四关的入场对白暂时为空。
- `Guide`：角色名称与默认立绘。
- `DialogueChinese`：由项目内 Noto Sans CJK 字体生成的 TMP 字体资源。

创建新资源可使用 Project 右键菜单 `Create > TapTap > Dialogue`。资源保存预设内容；当前句子位置由运行时的 `DialoguePlayer` 保存。

## 场景中的引用

`UIFlowController` 位于 Map 场景根目录，挂载 `DialoguePlayer` 与 `MapUIFlow`。播放器位于对话面板之外，关闭面板不会同时关闭播放器。

现有三个建造按钮与拆塔按钮的 On Click 绑定保持不变。`BuildSelection` 的 `Option Bindings` 明确对应：Button_1 → 铃铛，Button_2 → 笛子，Button_3 → 和弦。解锁状态和交互权限同时影响按钮显示及实际建造入口。

`NextButton` 和 `StartBattleButton` 的事件由脚本在运行时注册，不需要在 Inspector 再添加相同 On Click 回调。

`Canvas/BattleControlsRoot` 位于画面右上角，包含 `PauseButton` 和 `RestartButton`，只在战斗及暂停期间显示；它位于对话、结算、转场容器之前。按钮事件同样由 `MapUIFlow` 在运行时注册。

- 暂停：停止共享音乐时钟和游戏时间，保留敌人及防御塔，关闭建造与拆塔交互；按钮文字改为“继续”。加载音乐时暂停按钮暂不可用。
- 继续：从暂停的节拍恢复播放和敌人移动，恢复建造交互。
- 重新开始：清除当前测试入口生成的敌人、玩家本轮建造的塔及其运行时格子，停止音乐，返回当前关卡建造准备；保留当前解锁状态，不重播开场对话。暂停和音乐加载期间也可重新开始。
- BuildMenu 下三个建造按钮及拆塔按钮统一使用 `DialogueChinese` 字体与材质，原有 On Click 绑定保持不变。

## 当前完成范围

- 对话期间锁定地图选择、建造和拆塔。
- 对话结束进入准备阶段；结束对话的同一帧不接受地图操作。
- 初始只能建造铃铛塔；未解锁的塔按钮隐藏，直接调用建造方法也会被拒绝。
- 点击开始战斗后才启动共享时钟及原先启用的 `NorthWest entry 1` 测试入口；其他入口不自动开启。
- UI 布局已修正为底部对话框、全屏模态容器，文本关闭 Raycast Target。

## 尚未接入

正式有限波次、基地扣血与胜负判定、通关自动解锁、清图和镜头转场、关卡音乐配置切换尚未接入。血量文字明确标记为占位，战斗期间波次文字标记为测试。通关／失败对话资源已创建，但不会自行触发。

## 验证工具

- `Tools > UI Flow > Run Map UI Play Checks`：在已保存的 Map 场景中进行真实 Play 模式检查，结束后自动回到编辑模式。报告位于项目根目录 `Artifacts/UIFlow/play-check.txt`。
- `Tools > UI Flow > Add Battle Controls and Unify Fonts`：添加／更新战斗按钮并统一 BuildMenu 字体，不重置已有对话和 HUD 布局。
- `Tools > UI Flow > Configure Map UI and Placeholders`：重新应用初始布局与引用，不覆盖已存在的对话资源内容。此命令会重置指定 UI 对象的布局及文本外观，不要在完成自定义布局后随意重复执行。

首次接入前的场景与 BuildSelection 备份位于 `Artifacts/UIFlow`，仅用于恢复参考。
