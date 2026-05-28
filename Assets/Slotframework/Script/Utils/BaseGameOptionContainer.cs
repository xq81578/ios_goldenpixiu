#if !DISABLE_SRDEBUGGER
using System;
using System.Collections.Generic;
using Slot.Common;
using Slot.Common.UI;
using Spine;
using SRDebugger;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class BaseGameOptionContainer : IOptionContainer
{
    public PlatformData platformData;
    public ECurrency currency = ECurrency.PHP;
    public PlatformType platform = PlatformType.NONE;

    public bool IsDynamic => true;

    public event Action<OptionDefinition> OptionAdded;
    public event Action<OptionDefinition> OptionRemoved;

    public float ScrollDeltaPerTick = 0;

    public IEnumerable<OptionDefinition> GetOptions()
{
    int index = 0;
    List<OptionDefinition> options = new List<OptionDefinition>
    {
            // 切換幣值
        OptionDefinition.Create(
            "Currency",
            () =>
            {
                return currency;
            },
            (v) =>
            {
                if (currency != v)
                {
                    SwitchCurrency(v);
                }
                currency = v;
            },
            "Common", index++),
            // 切換平台
        OptionDefinition.Create(
            "Platform",
            () =>
            {
                return platform;
            },
            (v) =>
            {
                if (platform != v)
                {
                    SwitchPlatform(v);
                }
                platform = v;
            },
            "Common", index++),
            // 滾動增量每刻度
        OptionDefinition.Create(
            "ScrollDeltaPerTick",
            () =>
            {
                return ScrollDeltaPerTick;
            },
            (v) =>
            {
                ScrollDeltaPerTick = v;
                SetScrollDeltaPerTick();
            },
            "Common", index++),
            // 顯示滾動增量
        OptionDefinition.FromMethod(
            "Show Scroll Delta Per Tick",
            () => { ShowScrollDeltaPerTick(); },
            "Common", index++),
            // 取得 InfoPanel 滾動敏感度
        OptionDefinition.FromMethod(
            "Get InfoPanel Scroll Delta",
            () => { GetInfoScrollDeltaPerTick(); },
            "Common", index++)
        };

        return options;
    }

    public void SwitchCurrency(ECurrency currency)
    {
        platformData.SetCurrencyEnum(currency);
        new GameServiceInitEvent().Publish(this);
    }

    public void SwitchPlatform(PlatformType type)
    {
        new SetPlatformIdEvent(type).Publish(this);
    }

    public void SetScrollDeltaPerTick()
    {
        var inputSystemUIInputModule = CheckInputSystemUIInputModule();
        if (inputSystemUIInputModule == null)
            return;

        float originalValue = inputSystemUIInputModule.scrollDeltaPerTick;
        inputSystemUIInputModule.scrollDeltaPerTick = ScrollDeltaPerTick;
        LogUtils.Log("Set scroll delta per tick from " + originalValue + " to " + ScrollDeltaPerTick);
    }

    private void ShowScrollDeltaPerTick()
    {
        var inputSystemUIInputModule = CheckInputSystemUIInputModule();
        if (inputSystemUIInputModule == null)
            return;

        LogUtils.Log("Scroll delta per tick " + inputSystemUIInputModule.scrollDeltaPerTick);
    }

    private InputSystemUIInputModule CheckInputSystemUIInputModule()
    {
        var eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            LogUtils.LogWarning("EventSystem is null, cannot set scroll delta per tick.");
            return null;
        }

        var inputSystemUIInputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputSystemUIInputModule == null)
        {
            LogUtils.LogWarning("InputSystemUIInputModule is null, cannot set scroll delta per tick.");
            return null;
        }

        return inputSystemUIInputModule;
    }

    private void GetInfoScrollDeltaPerTick()
    {
        // 抓取場景所有的 InfoPanel.cs 元件
        InfoPanel[] infoPanels = GameObject.FindObjectsByType<InfoPanel>(FindObjectsSortMode.None);
        
        if (infoPanels == null || infoPanels.Length == 0)
        {
            LogUtils.Log("No InfoPanel found in the scene.");
            return;
        }

        foreach (var infoPanel in infoPanels)
        {
            // 取到其 ChildComponent 中的 ScrollRect 元件
            ScrollRect scrollRect = infoPanel.GetComponentInChildren<ScrollRect>();
            
            if (scrollRect != null)
            {
                // 並印出其 scrollSensitivity 屬性值
                LogUtils.Log($"InfoPanel: {infoPanel.gameObject.name}, ScrollSensitivity: {scrollRect.scrollSensitivity}");
            }
        }
    }
}
#endif