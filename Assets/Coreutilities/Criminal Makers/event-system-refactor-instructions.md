# Event System 重構指引

## 目標
以 Game Event Hub 取代原有 EventManager（TigerForge）架構。

### 事件
- 事件系統統一改用 Game Event Hub。

### 資料
- 新增 `PlatformData`、`GameStateData`、`PlayerBetData` 等類別，並以 DI（VContainer）管理。

---

## 事件更換步驟

### 1. 宣告事件類別

- 舊：EventManager 以字串識別事件。
- 新：Game Event Hub 以類別識別事件。

#### 範例：

`SpinTriggerEvent.cs`
```csharp
using CriminalMakers.GameEventHub;

public class SpinTriggerEvent : GameEvent { }
```

`LoadingProgressEvent.cs`
```csharp
using CriminalMakers.GameEventHub;

/// <summary>
/// 進度通知事件
/// </summary>
public class LoadingProgressEvent : GameEvent
{
    public float Progress { get; private set; }

    public LoadingProgressEvent SetProgress(float progress)
    {
        Progress = progress;
        return this;
    }
}
```

---

### 2. 監聽與取消監聽事件

#### 2.1 舊寫法（EventManager）

```csharp
// 監聽事件
EventManager.StartListening(Bottom_EventAction.UiSpinClick, OnSpin);
// 取消監聽事件
EventManager.StopListening(Bottom_EventAction.UiSpinClick, OnSpin);
```

#### 2.2 新寫法（Game Event Hub）

##### 靜態監聽（推薦用於頻繁事件）

```csharp
using CriminalMakers.GameEventHub;
using UnityEngine;

public class GameEventListener : MonoBehaviour
{
    private void OnEnable()
    {
        GameEventHub.Bind(this); // 啟用監聽
    }

    private void OnDisable()
    {
        GameEventHub.Unbind(this); // 關閉監聽
    }

    [OnGameEvent(SubscriberPriority.High)]
    private void OnSpinStatic(SpinTriggerEvent spinEvent)
    {
        Debug.Log("Spin event triggered!");
    }
}
```

##### 動態監聽（推薦用於一次性事件）

```csharp
private System.Action _onSpinListener;

private void Start()
{
    _onSpinListener = GameEventHub.Listen<SpinTriggerEvent>(this, OnSpinDynamic); // 監聽事件
}

private void OnSpinDynamic(SpinTriggerEvent spinEvent)
{
    // 處理事件
    Debug.Log("Spin event triggered!");
    _onSpinListener(); // 取消監聽
}
```

> 參考：`GameFlowManager` 的 `_onEventListener` 實作

---

### 3. 發送事件

#### 3.1 舊寫法
```csharp
EventManager.EmitEvent(Bottom_EventAction.UiSpinClick);
```

#### 3.2 新寫法
```csharp
// 方式一
new SpinTriggerEvent().Publish(this);

// 方式二
GameEventHub.Publish(this, new SpinTriggerEvent());
```

---

## 資料更換步驟

#### 舊寫法
```csharp
// 設值
EventManager.SetFloat(Bottom_EventData.GameCurrentBalance);
// 取值
EventManager.GetFloat(Bottom_EventData.GameCurrentBalance);
```

#### 新寫法
將資料分類於不同類別，並以 VContainer [Inject] 注入。
1. 宣告欄位（如 Bet 歸類於 PlayerBetData）
2. 直接向該類取值或設值

```csharp
[Inject]
private PlayerBetData _playerBetData;

private void Sample()
{
    // 取值
    var balance = _playerBetData.Balance;

    balance += 100;
    // 設值
    _playerBetData.SetBalance(balance);
}
```

---

## 資料類別與對應路徑

### PlatformData
- 檔案路徑：`Assets/SlotFramework/Script/Data/PlatformData.cs`
- 主要欄位：Id、CurrencyEnum、HomeUrl、RecordUrl
- 對應舊 key：平台ID、幣別、主頁網址、紀錄網址

### GameStateData

- 檔案路徑：`Assets/SlotFramework/Script/Data/GameStateData.cs`
- 主要欄位：
    - IsAuto
    - AutoSpinTotalSpins
    - PlayerTurboTypeEnum
    - TurboTypeEnum
    - IsFreeGame
    - IsSpinLock
    - FreeGameRoundIndex
    - FreeGameSpinTempCount
- 對應舊 key：
    - GameAuto（是否自動旋轉）
    - AutoSpinTotalSpins（自動旋轉總次數）
    - PlayerTurbo（玩家選擇的快速模式）
    - GameTurbo（當前快速模式）
    - GameIsInFreeGame（是否在免費遊戲）
    - GameFreeGameRoundIndex（免費遊戲回合索引）
    - GameFreeGameSpinTempCount（免費遊戲暫存次數）
    - GameSpinLock（是否鎖定 Spin）

### PlayerBetData

- 檔案路徑：`Assets/SlotFramework/Script/Data/PlayerBetData.cs`
- 主要欄位：
    - Bet
    - BetRange
    - Balance
    - IsExtraBet
    - ExtraBetRatio
    - BuyTypeEnum
- 對應舊 key：
    - GameCurrentBet（當前投注金額）
    - GameBetList（可選投注範圍）
    - GameCurrentBalance（玩家餘額）
    - GameIsExtraBet（是否啟用額外投注）
    - GameExtraBetRatio（額外投注倍率）
    - GameBuyType（購買類型）

### WinSettleData

- 檔案路徑：`Assets/SlotFramework/Script/Data/WinSettleData.cs`
- 主要欄位：
    - CurrentWin
    - AddWin
    - SetWin
- 對應舊 key：
    - UiCurrentWin（當前贏分）
    - UiAddWin（新增贏分）
    - UiSetWin（結算後總贏分）

---

## VContainer Inject
- 至遊戲主 Scene，將直橫版 BottomBar 和 BuyTypeButtons 拉至 Auto Inject GameObjects。

## 修改範例

**舊寫法：**
```csharp
EventManager.EmitEvent(Bottom_EventAction.SetBottomUiFreeGameEnter);
```

**新寫法：**
```csharp
new FreeGameEnterEvent().Publish(this);
```
---

**舊寫法：**
```csharp
EventManager.EmitEvent(Bottom_EventAction.SetBottomUiFreeGameEnter);
```

**新寫法：**
```csharp
new FreeGameEnterEvent().Publish(this);
```
---

**舊寫法：**
```csharp
EventManager.EmitEvent(Bottom_EventAction.SetBottomUiLock);
```

**新寫法：**
```csharp
new UIBottomLockEvent().Publish(this);
```
---

**舊寫法：**
```csharp
EventManager.EmitEvent(Bottom_EventAction.SetBottomUiNormal);
```

**新寫法：**
```csharp
new UIBottomNormalEvent().Publish(this);
```
---

**舊寫法：**
```csharp
EventManager.EmitEvent(Bottom_EventAction.UiSetWin, 0);
```

**新寫法：**
```csharp
_winSettleData.SetSetWin(0f);
new UISetWinEvent().Publish(this);
```

---

**舊寫法：**
```csharp
EventManager.EmitEventData(Bottom_EventAction.UiSetWinTempEffect, 1);
EventManager.EmitEventData(Bottom_EventAction.UiSetWinTemp, _currentWin);
```

**新寫法：**
```csharp
new SetWinTempEffectEvent(WinTemp.EffectEnum.CountUp).Publish(this);
new SetWinTempEvent(_currentWin).Publish(this);
```
---

**舊寫法：**
```csharp
EventManager.EmitEvent(Bottom_EventAction.SetBottomUiFreeGameLeave);
```

**新寫法：**
```csharp
new FreeGameLeaveEvent().Publish(this);
```

---

**舊寫法：**
```csharp
private void Function()
{
    EventManager.EmitEventData(Bottom_EventAction.UiSetWin, 0f);
}
```

**新寫法：**
```csharp
[Inject]
private WinSettleData _winSettleData;

private void Function()
{
    _winSettleData.SetSetWin(0f);
    new UISetWinEvent().Publish(this);
}
```
---

**舊寫法：**
```csharp
public float Bet { get => EventManager.GetFloat(Bottom_EventData.GameCurrentBet); }
```

**新寫法：**
```csharp
[Inject]
private PlayerBetData _playerBetData;

public float Bet { get => _playerBetData.Bet; }
```
---

**舊寫法：**
```csharp
EventManager.EmitEvent(Bottom_EventAction.UiMarqueeSpecial);
```

**新寫法：**
```csharp
new UIMarqueeSpecialEvent().Publish(this);
```
---
