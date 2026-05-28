using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class DialogButton : MonoBehaviour
{
    [SerializeField]
    protected List<DialogBtnStyle> _styleObjects;
    [SerializeField]
    protected UnityEvent<string> _onUpdateBtnText;

    protected Action<int> _onClickButton;
    private int _index;

    public void Init(int index, string btnText, Action<int> clickAction, string style = "")
    {
        _onClickButton = clickAction;
        _index = index;
        gameObject.name = $"{nameof(gameObject)}_{index}";
        _onUpdateBtnText?.Invoke(btnText);
        style = string.IsNullOrEmpty(style) ? _styleObjects[0].style : style;
        UpdateDisplay(style);
    }

    public void OnClick()
    {
        _onClickButton?.Invoke(_index);
    }

    protected virtual void UpdateDisplay(string style)
    {
        for (int i = 0; i < _styleObjects.Count; i++)
        {
            _styleObjects[i].styleObject.SetActive(_styleObjects[i].style == style);
        }
    }
}

[Serializable]
public class DialogBtnStyle
{
    public string style;
    public GameObject styleObject;
}