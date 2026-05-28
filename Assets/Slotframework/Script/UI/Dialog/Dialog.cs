using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Pool;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.SmartFormat.PersistentVariables;

public class ActionButton
{
    public string text;
    public Action action;
    public string style;

    public ActionButton(string text, Action action = null, string style = "")
    {
        this.text = text;
        this.action = action;
        this.style = style;
    }
}

public class Dialog : MonoBehaviour
{
    private const string DefaultTitle = "System Notice";
    private const string DefaultMessage = "Network connection error. Please reconnect.";

    [SerializeField]
    private GameObject _window;
    [SerializeField]
    private TextMeshProUGUI _titleText;
    [SerializeField]
    private TextMeshProUGUI _messageText;
    [SerializeField]
    private GameObject _btnContainer;
    [SerializeField]
    private DialogButton _dialogButtonPrefab;
    [SerializeField]
    private Button _closeBtn;
    [SerializeField]
    private Button _backgroundBtn;

    private List<ActionButton> _actionButtons;
    private ObjectPool<DialogButton> _btnPool;
    private DialogMediator _mediator;
    private LocalizeStringEvent _titleLocalize;
    private LocalizeStringEvent _messageLocalize;
    private List<StringVariable> messageValues = new List<StringVariable>();

    private void Awake()
    {
        if (_window == null)
            _window = gameObject;
        _actionButtons = new List<ActionButton>();

        _btnPool = new(
            () =>
            {
                // 物件池創建物件的時候，執行的動作
                var btn = Instantiate(_dialogButtonPrefab);
                btn.transform.SetParent(_btnContainer.transform, false);
                btn.transform.localScale = Vector3.one;
                btn.gameObject.SetActive(false);
                return btn;
            },
            (btn) =>
            {
                // 物件池拿取物件的時候，執行的動作
                btn.gameObject.SetActive(true);
            },
            (btn) =>
            {
                // 物件池回收物件的時候，執行的動作
                btn.gameObject.SetActive(false);
            },
            (btn) =>
            {
                // 物件池銷毀物件的時候，執行的動作
                Destroy(btn.gameObject);
            },
            true,
            2
        );
    }

    public void Init(DialogMediator mediator)
    {
        _mediator = mediator;
        _closeBtn.onClick.AddListener(mediator.OnClose);
        _backgroundBtn.onClick.AddListener(mediator.OnClickBackground);

        _titleLocalize = _titleText.GetComponent<LocalizeStringEvent>();
        _messageLocalize = _messageText.GetComponent<LocalizeStringEvent>();

        _titleText.text = "";
        _messageText.text = "";
    }

    public void ShowDialog(string title, string message, ActionButton act = null, bool showCloseBtn = true)
    {
        ActionButton[] acts = act != null ? new ActionButton[] { act } : null;
        ShowDialog(title, message, acts, showCloseBtn);
    }

    public void ShowDialog(string title, string message, ActionButton[] acts = null, bool showCloseBtn = true)
    {
        title = string.IsNullOrWhiteSpace(title) ? DefaultTitle : title;
        message = string.IsNullOrWhiteSpace(message) ? DefaultMessage : message;

        _titleText.text = title;
        _messageText.text = message;
        _titleText.gameObject.SetActive(true);
        _messageText.gameObject.SetActive(true);
        _closeBtn.gameObject.SetActive(showCloseBtn);
        _actionButtons.Clear();
        DeleteButtons();
        if (acts != null)
        {
            for (int i = 0; i < acts.Length; i++)
            {
                CreateButton(acts[i], i);
            }
        }
        OnShow();
    }

    // 多語系版本
    public void ShowDialog(string titleTable, string titleKey, string messageTable, string messageKey, ActionButton act = null, bool showCloseBtn = true)
    {
        ActionButton[] acts = act != null ? new ActionButton[] { act } : null;
        ShowDialog(titleTable, titleKey, messageTable, messageKey, acts, showCloseBtn);
    }

    // 多語系版本
    public void ShowDialog(string titleTable, string titleKey, string messageTable, string messageKey, ActionButton[] acts = null, bool showCloseBtn = true)
    {
        _messageLocalize.enabled = true;
        _titleLocalize.StringReference.SetReference(titleTable, titleKey);
        _messageLocalize.StringReference.SetReference(messageTable, messageKey);
        _closeBtn.gameObject.SetActive(showCloseBtn);
        _actionButtons.Clear();
        DeleteButtons();
        if (acts != null)
        {
            for (int i = 0; i < acts.Length; i++)
            {
                CreateButton(acts[i], i);
            }
        }
        OnShow();
    }

    // 多語系版本 + string.Format
    public void ShowDialog(string titleTable, string titleKey, string messageTable, string messageKey, object[] value, ActionButton[] acts = null, bool showCloseBtn = true)
    {
        _titleText.text = "";
        _messageText.text = "";

        _messageLocalize.enabled = true;
        _titleLocalize.StringReference.SetReference(titleTable, titleKey);
        _messageLocalize.StringReference.SetReference(messageTable, messageKey);

        messageValues.Clear();
        for (int i = 0; i < value.Length; i++)
        {
            if (messageValues.Count < value.Length)
            {
                messageValues.Add(new StringVariable());
            }
            if (!_messageLocalize.StringReference.TryGetValue(i.ToString(), out var outValue))
            {
                _messageLocalize.StringReference.Add(i.ToString(), messageValues[i]);
            }
            else
            {
                messageValues[i] = outValue as StringVariable;
            }
            messageValues[i].Value = value[i].ToString();
        }

        _closeBtn.gameObject.SetActive(showCloseBtn);
        _actionButtons.Clear();
        DeleteButtons();
        if (acts != null)
        {
            for (int i = 0; i < acts.Length; i++)
            {
                CreateButton(acts[i], i);
            }
        }
        OnShow();
    }

    public void HideDialog()
    {
        OnHide();
    }

    protected virtual void OnShow()
    {
        _window.SetActive(true);
    }

    protected virtual void OnHide()
    {
        _window.SetActive(false);
    }

    private void CreateButton(ActionButton actButton, int index)
    {
        var dialogButton = _btnPool.Get();
        dialogButton.Init(index, actButton.text, (idx) => { _mediator.OnClickButton(idx); }, actButton.style);
        _actionButtons.Add(actButton);
    }

    private void DeleteButtons()
    {
        foreach (DialogButton btn in _btnContainer.GetComponentsInChildren<DialogButton>())
        {
            _btnPool.Release(btn);
        }
    }
}
