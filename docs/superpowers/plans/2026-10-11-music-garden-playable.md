# 夜之乐章可玩版计划

Spec: docs/design/夜之乐章_PRD_音乐塔防与涌现_v0.1_2026-10-11.md

用户已要求执行。保留现有未提交成果，在 Assets/MusicGarden 下独立实现，避免覆盖旧教学关。

## Global constraints

- 四区25×13地形逐格保留；整体50×26。道路不能重画。关卡顺序NW、NE、SE、SW。
- 普通3×3与和弦5×5方形采用PRD推荐；Inspector可切换附件的菱形模板。射程允许重叠。
- 击杀才发声、领奏先于伴奏、80ms预调度、真实日志回放，金币不靠音乐无限增殖。
- UI、地块、建造位、配置、预设琴塔保存为团结可编辑对象。Windows可执行包。
- 不自动提交或推送，保护现有脏工作区。

## Task 1: 规则与地图

Produces: GardenSimulation、RegionDefinition、MapRules、真实击杀日志。
先用可编译空壳观察道路/LOS/领奏/共鸣测试失败，再实现。验证路径连续且只走道路；LOS含角接触；单杀单奖励；单和弦不触发共鸣；共鸣冷却与出生免疫；领奏先处理；重试经济重置；四区自动方案可存活。
Expected: 新核心测试全部通过，旧34项仍通过。

## Task 2: 生产美术与编辑场景

Consumes: 精确地图与路径；Produces: Sprite、Prefab、GardenConfig、MusicGarden.unity。
内置imagegen生成透明角色图集与地块图集，Unity数学切片。1300个独立可编辑地块，真实草地建造位。保留原概念图。
Expected: batch编辑器导入成功，325格/区，地图对位零差异，场景与Canvas保存。

## Task 3: 可玩控制与界面

Consumes: 核心与场景；Produces: GardenGame、GardenView、GardenHUD。
四区整备、两段/三段波次、暂停、领奏预约、筛选/节拍、回收、全局回放。Scene中的Canvas使用真实可修改组件。
Expected: 音符与呈现同拍；锁定/暂停不重复事件；空格不触发焦点按钮两次；失败可重试。

## Task 4: 验证与交付

Produces: 编辑器功能检查、截图、Windows包、使用说明。
验证真实运行控制器和UI交互及分区流程，记录检查结果，实际Windows进程启动与截图。最终做一次独立代码审查并修复重要问题。
Expected: 核心/编辑器检查通过、无运行异常、构建Succeeded、可见可玩截图。

## Review focus

暂停预调度边界、UI焦点、回放跨区域偏移、Scene编辑数据是否真的被读取、精确道路保留、视线与预览一致、没有单和弦共鸣或双金币。
