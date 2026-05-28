using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Slot.Common;

public class DialogMediator : Singleton<DialogMediator>
{
    [SerializeField, FoldoutGroup("直橫版UI")]
    protected Dialog _landscapeUI;
    [SerializeField, FoldoutGroup("直橫版UI")]
    protected Dialog _portraitUI;

    private List<ActionButton> _actionButtons;
    private bool _needCloseByTapBG;

    protected override void Awake()
    {
        base.Awake();
        _landscapeUI?.Init(this);
        _portraitUI?.Init(this);
    }

    public void DynamicSetLandScapeUI(GameObject gameObject)
    {
        Dialog dialog = gameObject.GetComponent<Dialog>();
        _landscapeUI = dialog;
        _landscapeUI.Init(this);
    }

    public void DynamicSetPortraitUI(GameObject gameObject)
    {
        Dialog dialog = gameObject.GetComponent<Dialog>();
        _portraitUI = dialog;
        _portraitUI.Init(this);
    }

    public static void ShowDialog(string title, string message, ActionButton act = null, bool showCloseBtn = true, bool needCloseByTapBG = false)
    {
        ActionButton[] acts = act != null ? new ActionButton[] { act } : null;
        ShowDialog(title, message, acts, showCloseBtn, needCloseByTapBG);
    }

    public static void ShowDialog(string title, string message, ActionButton[] acts = null, bool showCloseBtn = true, bool needCloseByTapBG = false)
    {
        if (Instance == null)
            return;

        Instance._needCloseByTapBG = needCloseByTapBG;
        Instance._actionButtons = acts != null ? new List<ActionButton>(acts) : new List<ActionButton>();
        Instance._landscapeUI?.ShowDialog(title, message, acts, showCloseBtn);
        Instance._portraitUI?.ShowDialog(title, message, acts, showCloseBtn);
    }

    // 多語系版本
    public static void ShowDialog(string titleTable, string titleKey, string messageTable, string messageKey, ActionButton act = null, bool showCloseBtn = true, bool needCloseByTapBG = false)
    {
        ActionButton[] acts = act != null ? new ActionButton[] { act } : null;
        ShowDialog(
            titleTable, titleKey, messageTable, messageKey,
            acts, showCloseBtn, needCloseByTapBG);
    }

    // 多語系版本
    public static void ShowDialog(string titleTable, string titleKey, string messageTable, string messageKey, ActionButton[] acts = null, bool showCloseBtn = true, bool needCloseByTapBG = false)
    {
        if (Instance == null)
            return;

        Instance._needCloseByTapBG = needCloseByTapBG;
        Instance._actionButtons = acts != null ? new List<ActionButton>(acts) : new List<ActionButton>();
        Instance._landscapeUI?.ShowDialog(
            titleTable, titleKey, messageTable, messageKey,
            acts, showCloseBtn);
        Instance._portraitUI?.ShowDialog(
            titleTable, titleKey, messageTable, messageKey,
            acts, showCloseBtn);
    }

    // 多語系版本 + string.Format
    public static void ShowDialog(string titleTable, string titleKey, string messageTable, string messageKey, object[] value, ActionButton act = null, bool showCloseBtn = true, bool needCloseByTapBG = false)
    {
        if (Instance == null)
            return;

        ActionButton[] acts = act != null ? new ActionButton[] { act } : null;
        Instance._needCloseByTapBG = needCloseByTapBG;
        Instance._actionButtons = acts != null ? new List<ActionButton>(acts) : new List<ActionButton>();
        Instance._landscapeUI?.ShowDialog(
            titleTable, titleKey, messageTable, messageKey, value,
            acts, showCloseBtn);
        Instance._portraitUI?.ShowDialog(
            titleTable, titleKey, messageTable, messageKey, value,
            acts, showCloseBtn);
    }

    public static void HideDialog()
    {
        if (Instance == null)
            return;

        Instance._landscapeUI?.HideDialog();
        Instance._portraitUI?.HideDialog();
    }

    public void OnClickButton(int idx)
    {
        LogUtils.Log("OnClickButton: " + idx);
        ActionButton btn = _actionButtons[idx];
        btn.action?.Invoke();
        AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button);
        // 按下任何按鈕都會關閉對話框
        HideDialog();
    }

    public void OnClose()
    {
        AudioManager.PlayEffectByName(Bottom_AudioName.Se_Button);
        HideDialog();
    }

    public void OnClickBackground()
    {
        if (_needCloseByTapBG)
        {
            HideDialog();
        }
    }
}
