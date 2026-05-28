using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

public class UIOrientationMediator<T> : MonoBehaviour
{
    [SerializeField, BoxGroup("直橫版UI")]
    protected T _landscapeUI;

    [SerializeField, BoxGroup("直橫版UI")]
    protected T _portraitUI;
    public T LandscapeUI { get => _landscapeUI; set { _landscapeUI = value; ApplyUI(); } }
    public T PortraitUI { get => _portraitUI; set { _portraitUI = value; ApplyUI(); } }
    protected List<T> AllUIs => _uiList;
    protected T ActiveUI => Screen.width > Screen.height ? _landscapeUI : _portraitUI;
    private List<T> _uiList = new();
    protected Action AfterLoadAction;

    protected virtual void Awake()
    {
        ApplyUI();
    }

    protected virtual void Start()
    {
        Initialize();
    }

    private void ApplyUI()
    {
        if (_landscapeUI != null && !_uiList.Contains(_landscapeUI)) _uiList.Add(_landscapeUI);
        if (_portraitUI != null && !_uiList.Contains(_portraitUI)) _uiList.Add(_portraitUI);

        if (_landscapeUI != null && _portraitUI != null)
        {
            AfterLoadAction?.Invoke();
        }
    }

    protected virtual void Initialize() { }

    /// <summary>
    /// e.g. InvokeAllUIs(ui => ui.SomeMethod());
    /// </summary>
    /// <param name="action"></param>
    protected void InvokeAllUIs(Action<T> action)
    {
        foreach (var ui in _uiList)
        {
            action(ui);
        }
    }

    /// <summary>
    /// e.g. var results = InvokeAllUIs(ui => ui.SomeMethod());
    /// </summary>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="func"></param>
    /// <returns></returns>
    protected IEnumerable<TResult> InvokeAllUIs<TResult>(Func<T, TResult> func)
    {
        foreach (var ui in _uiList)
        {
            yield return func(ui);
        }
    }

    /// <summary>
    /// e.g. await InvokeAllUIsAsync(ui => ui.SomeAsyncMethod());
    /// </summary>
    /// <param name="func"></param>
    /// <returns></returns>
    protected async UniTask InvokeAllUIsAsync(Func<T, UniTask> func)
    {
        await UniTask.WhenAll(_uiList.Select(func));
    }
}
