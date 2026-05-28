# Slot001_GoldenPixiu 代码导览与流程说明


## 架构总览

- 依赖注册：`GameLifetimeScope` 在场景中注册核心组件与 `ScriptableObject` 实例，供 VContainer 注入。
- 流程驱动：`SlotStateMachine` 监听事件并驱动状态流转（Idle → Spin → Rotate → StopSpin → ShowWin → CheckFreeGame → …）。
- 业务逻辑：`GameLogic` 计算下注、转化盘面数据、判断免费游戏触发、计算赢分。
- 网络服务：`GameService` 封装发包与回包处理，更新 `GameData`。
- 表现层分离：
  - `ReelControllerMediator` 作为中介者统一调度横竖屏 `ReelController`
  - `FlowPresenter` 协调 UI 表演流程（赢分结算、免费游戏转场等）
  - 各 UI 脚本只负责自身展示，`Mediator` 做双端联动与对外接口
- 资源与配置：`BoardDataSO` 初始盘面、`SymbolDataSO` 符号配置、`ReelStripGroupSO` 转轮带数据、`PayTableSO` 赔率与总押注计算

---

## 核心流程（状态机）

文件：`Script/SlotStateMachine.cs`

- 状态枚举：`Init`、`Idle`、`Spin`、`Rotate`、`StopSpin`、`ShowWin`、`CheckFreeGame`、`FreeGame`、`FreeGameSummary`、`EndFreeGame`、`EndGame`
- 生命周期
  - `Init_Enter`：播放主背景音，初始化 `GameData`，设定初始盘面（`BoardDataSO.RandomBoard`），进入 `Idle`
  - `Idle_Enter/Update`：
    - UI 置为正常底栏状态（非免费局）
    - 根据 `AutoSpinSetting` 判断是否自动旋转，定时触发进入 `Spin`
    - 玩家触发 `SpinTriggerEvent` 时置位 `_isSpinTriggered`
  - `Spin_Enter`：
    - 免费局中：递增已完成免费次数、锁 UI、重置 Ways 文本，然后进入 `Rotate`
    - 普通局：锁 UI、减少自动旋次数、计算总押注（`GameLogic.GetTotalBet`）、`SendSpinRequest` 发包、重置一手数据与 UI，再进入 `Rotate`
  - `Rotate_Enter/Update`：
    - 根据是否免费局选择转轮带组（`_mgReelStripGroupData` / `_fgReelStripGroupData`）
    - `ReelControllerMediator.StartRotation` 启动旋转（可 Turbo）
    - 等到 `CanStop` 进入 `StopSpin`
  - `StopSpin_Enter`：
    - 取当前 `TumbleResult` → 转为 `BoardData`（`GameLogic.Trans2BoardData`）
    - 随机生成停轮位置（`GameLogic.GetCurrentEndPos`）
    - 生成组合转轮数据（`GameLogic.GetCombReelsByEndBoard`），调用 `ReelControllerMediator.StopRotation`
    - 完成后进入 `ShowWin`
  - `ShowWin_Enter`：
    - 取本手赢分（免费/普通分支），`FlowPresenter.ShowTotalWin` 做表演，进入 `CheckFreeGame`
  - `CheckFreeGame_Enter`：
    - 若回包含免费次数，则计算新获得次数，进入 `FreeGame`；否则在免费局中判断去 `FreeGameSummary` 或回到 `Idle`；普通局则进入 `EndGame`
  - `FreeGame_Enter`：
    - 播放免费入场引导（铃声、动画），打开“获得免费旋转”面板（Retrigger 做加号），进入 `Idle`
  - `FreeGameSummary_Enter`：
    - 展示本次免费总赢分（`FlowPresenter.ShowSettleTotalWin`），进入 `EndFreeGame`
  - `EndFreeGame_Enter`：
    - 退出免费局，恢复普通盘面，进入 `EndGame`
  - `EndGame_Enter`：
    - 更新上一手总赢分、同步余额（`SetBalance`），回到 `Idle`
- 网络交互
  - `SendSpinRequest` → `GameService.SendSpin`，回调 `OnSpinResponse(JToken)`
  - 回包反序列化至 `SlotResult`，填充 `GameData.SlotResult` 等

---

## 数据模型

- `Model/BoardData.cs`
  - `BoardData`：盘面（每列 `ReelData`）
    - `GetPossibleWays()`：逐列累计 Ways，用于展示与演出
    - `GetScatterAccumulation()`：统计落地过程中 Scatter 累计，用于瞇牌触发
  - `ReelData`：当前列的符号序列，支持从 `ReelStripGroupSO` 随机生成组合带（`GetRandomCombReelDataList`）
  - `CellData`：单格数据（`Id`/`Name`/`IsWild`/`IsScatter`），支持转 Wild
- `Model/BoardDataSO.cs`
  - `ScriptableObject` 持盘面列表，`RandomBoard()` 通过 `this.Clone()` 拿一份副本（克隆扩展来自通用工具）
- `Model/GameData.cs`
  - 核心状态持有者（通过 `[Inject]` 注入 `PlayerBetData`、`GameStateData`）
  - 普通/免费局切换、每手步骤索引、倍数、免费旋转统计、余额与下注
  - 提供 `Initialize`、`ResetMultiplier`、`ResetNewRound` 等
- `Model/SlotResult.cs`
  - 网络回包的简化数据结构：`balance`、`TotalWin`、`MGResult`、`FGResult`、`TumbleResult` 等（注意与 Protobuf 生成版本的不同）
- `Model/SlotDef.cs`
  - Protobuf 自动生成（非常大），定义 `CellSymbol`、`ReelSymbol`、`SpinResult`、`FGResult`、`SlotResult` 等类型的序列化结构
- `Model/SymbolDataSO.cs`
  - 符号配置基类的具体化，`SymbolData` 扩展 `SymbolType`（Normal/Scatter/Wild/NN）

---

## 服务与逻辑

- `GameLifetimeScope.cs`
  - 注册 `SlotStateMachine`、各 Mediator、`GameData`、`GameLogic`、`FlowPresenter`
  - 注入 `SymbolDataSO`、`PayTableSO` 实例（来自资源）
- `GameService.cs`
  - 继承 `BaseGameService`
  - 初始化挂载 `SRDebugger.OptionContainer` 动态面板（演示/预设 RTP）
  - `SendSpin` 走基类发包（`GAME_ID="Slot001"`），支持在调试面板触发 `SpinTriggerEvent`
- `OptionContainer.cs`
  - SRDebugger 动态选项容器，提供多种 RTP/表演预设（如 FourScatter、BigWin 等），支持 Demo 切换
- `GameLogic.cs`
  - 赔率与总押注：`GetTotalBet`（依赖 `PayTableSO`）
  - 回包拆解：当前 `TumbleResult`、当前手赢分（普通/免费）
  - 盘面构造与组合带融合：`Trans2BoardData`、`GetCombReelsByEndBoard`
  - 免费局判定：`CheckFreeGame`（按回包 `FGSpinCount`）、`CalculateFreeSpinsWon`（示例返回 8）
  - 统计工具：Scatter 数量、加总赢分（`ServiceUtils` 进行客户端/服务端金额换算）

---

## 控制器与中介者

- `ReelControllerMediator.cs`
  - 横竖屏统一调度，暴露 `StartRotation`/`StopRotation`/`ClearSymbols`/`DropSymbols`/`RefillSymbols`
  - `StartRotation`：生成随机组合带，播放起始音效，双路 UI 同步启动
  - `StopRotation`：计算 Ways 与 Scatter 累计，并行停轮，处理瞇牌特效与 TopRow 特殊，结束标记 `_isRotating=false`
  - 强制停止：置位两个 UI 的 `IsForceStop`
- `ReelController.cs`
  - 单端转轮表现实现：旋转速度、停轮间隔、一手持续时间、瞇牌阈值与速度
  - `StartRotation`：清除特效、逐列按速度旋转；Turbo 模式直接允许停轮
  - `StopRotation`：按列停轮；满足条件触发瞇牌（播放 `se_squat`，顶行遮罩），展示 Ways，处理强停音效与遮罩
  - 清除/掉落/补符：按列组织任务并行执行，含黑遮罩管理
  - Scatter 演出：`PlayScatterWinFx`/`StopScatterFx`
- `SymbolPool.cs`
  - `GenericPool<Symbol>` 特化，符号对象池

---

## Presenter 与 UI

- `Presenter/FlowPresenter.cs`
  - 赢分临时显示与结算动画：发布事件、倍数公式展示、音效、数值动画
  - 免费游戏引导：铃声、角色入场、转场面板、背景音乐切换
  - 免费游戏场景切换：演出任务组合（`GamePlayUI.PlayFreeGameShow`）、过渡 UI、完成后转为免费局 UI
  - 总赢/结算展示：`WinCelebrationUIMediator.ShowTotalWin`、`SettleUIMediator.ShowSettleTotalWin`
- `View/GamePlayUI.cs`
  - 背景/角色/免费入场 Spine，Ways 文本与倍数演出（DOTween）
  - FreeGame Entry/Show 动画，`TransGamePlayUI` 切换 MG/FG 背景
  - 注意：`ResetWays`/`SetWays`/`SetMultiplier` 当前为空实现，需要完善
- `View/GamePlayUIMediator.cs`
  - 新手一手重置：Win、Ways、Multiplier 文本
  - 更新 Ways、倍数、免费入场/倍数角色演出（含音效），切换 MG/FG 并发布进入/离开事件
- `View/ReelStrip.cs`
  - 符号布局与移动：支持四向滚动、位置校准、回弹烟雾效果
  - 旋转到停位：根据 `_reelSpaceSize` 和 `_endShowReelStripCount` 计算补符与展示数量
  - 清除/掉落/补符：并行移动与对象池管理、遮罩控制、瞇牌展示/关闭
- `View/Symbol.cs`
  - 符号初始化（`SymbolDataSO`）、正常/模糊状态切换、Scatter 演出、清除标记、移动动画与遮罩管理
  - Debug：`OnGUI` 展示符号名（开发用）
- `View/FreeSpinsUI.cs` 与 `View/FreeSpinsUIMediator.cs`
  - 获得免费旋转面板：横竖屏 Spine 动画，Retrigger 显示加号；Mediator 控制打开/关闭与状态
- `View/SettleUI.cs` 与 `View/SettleUIMediator.cs`
  - 免费结算面板：总赢文本、横竖屏 Spine 动画，自动关闭支持取消，音效控制

注：`WinCelebrationUIMediator` 在 `GameLifetimeScope` 注册，但不在本目录，位于共享或框架目录；`UIOrientationMediator<T>` 与 `GenericPool<T>` 属于通用基础工具，位于 `CoreUtilities` 或 `SlotFramework`。

---

## 事件与音效

- 事件
  - UI 底栏：`UIBottomNormalEvent`、`UIBottomLockEvent`、`UIAddWinEvent` 等
  - 游戏流：`GameReadyEvent`、`GameUIReadyEvent`、`SpinTriggerEvent`、`FreeGameEnterEvent`、`FreeGameLeaveEvent` 等
  - 结算：`SetWinTempEvent`、`SettleWinTempEvent`、`SettleWinTempEndEvent`
- 音效与音乐（示例）
  - 起停：`se_start`、`se_stop`
  - Scatter：`se_scatter`、`se_squat`、`se_scatter_ring`
  - 赢分：`se_regularwin`、`se_mul_win`、`se_mul_get`
  - 背景：`mu_main_background`、`mu_free_background`、`mu_trans_background`、`mu_congrats`

---

## （普通局）

1. `GameReadyEvent` → 状态机进入 `Init`，初始化盘面 → 进入 `Idle`
2. 玩家点击或自动旋转 → `SpinTriggerEvent` → 状态机进入 `Spin`
3. 锁 UI、计算总押注 → `GameService.SendSpin` 发包 → 重置本手数据 → 进入 `Rotate`
4. 根据 MG 选择转轮带 → `ReelControllerMediator.StartRotation` 启动旋转
5. 满足停轮条件 → 进入 `StopSpin`
   - 取 `TumbleResult` → 转为 `BoardData`
   - 随机停位 → 融合组合带 → `StopRotation`（瞇牌/遮罩/Ways 展示）
6. 进入 `ShowWin` → `FlowPresenter.ShowTotalWin` 表演本手赢分
7. 进入 `CheckFreeGame` → 无免费则进入 `EndGame`
8. `EndGame`：更新上一手总赢分、同步余额 → 返回 `Idle`

免费局触发或 Retrigger 时序参照 `FreeGame`、`FreeGameSummary`、`EndFreeGame` 三个状态。

---

## 扩展建议与注意

- `GamePlayUI.ResetWays/SetWays/SetMultiplier` 为空实现，需按 UI 需求填充
- `GameLogic.CalculateFreeSpinsWon` 当前返回固定值（8），实际应按 Scatter 数量与策划表计算
- `ReelStripGroupSO`、`PayTableSO`、`ServiceUtils`、`GameStateData` 等位于框架/资源，需要统一维护
- `SlotResult` 与 `SlotDef` 存在两套数据结构，确保网络解析一致性（JSON/Protobuf）
- 调试用 `OptionContainer` 提供预设场景，适合排查演出与状态机流程
- 关注 `IsTurbo`、`IsForceStop` 对停轮与音效播放的影响；并行任务合理等待与取消尤为重要

---

## 文件索引与职责

- 核心
  - `GameLifetimeScope.cs`：依赖注册与生命周期配置
  - `SlotStateMachine.cs`：状态机与整手流程
  - `GameService.cs`：网络服务、调试集成
  - `GameLogic.cs`：玩法与数据计算
  - `Presenter/FlowPresenter.cs`：表演流程协调
- 数据
  - `Model/BoardData.cs`、`BoardDataSO.cs`：盘面结构与初始盘面
  - `Model/GameData.cs`：游戏状态与数据容器
  - `Model/SlotResult.cs`、`Model/SlotDef.cs`：网络回包模型
  - `Model/SymbolDataSO.cs`：符号配置
- 控制器与中介者
  - `ReelController.cs`、`ReelControllerMediator.cs`：转轮表现与双端调度
  - `SymbolPool.cs`：符号对象池
- UI
  - `View/GamePlayUI.cs`、`View/GamePlayUIMediator.cs`：玩法主 UI 与调度
  - `View/ReelStrip.cs`：转轮带可视实现
  - `View/Symbol.cs`：符号可视逻辑
  - `View/FreeSpinsUI.cs`、`View/FreeSpinsUIMediator.cs`：免费入场面板
  - `View/SettleUI.cs`、`View/SettleUIMediator.cs`：免费结算面板

---

## 快速上手

- 打开场景并确保 `GameLifetimeScope` 对象在场景中，检查其 `SymbolDataSO`、`PayTableSO` 赋值
- 在状态机对象上检查 `BoardDataSO`、`ReelStripGroupSO` 的 MG/FG 配置是否齐全
- 运行后可用 `SRDebugger` 的 `OptionContainer` 模拟常见场景（如 Scatter、BigWin）
- UI 侧如需调整 Ways/Multiplier 文本与表现，在 `GamePlayUI` 中完善对应方法