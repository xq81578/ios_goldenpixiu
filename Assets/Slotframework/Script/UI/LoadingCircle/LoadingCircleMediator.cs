using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class LoadingCircleMediator : Singleton<LoadingCircleMediator>
{
    [SerializeField, FoldoutGroup("直橫版UI")]
    protected LoadingCircle _landscapeUI;
    [SerializeField, FoldoutGroup("直橫版UI")]
    protected LoadingCircle _portraitUI;

    private void Start()
    {
        HideLoadingCircle();
    }

    public static void ShowLoadingCircle(float delay = 0f)
    {
        if (Instance == null)
            return;

        Instance._landscapeUI?.ShowLoadingCircle(delay);
        Instance._portraitUI?.ShowLoadingCircle(delay);

        if (EventSystem.current != null &&
            EventSystem.current.TryGetComponent<InputSystemUIInputModule>(out var inputSystemUIInputModule))
            inputSystemUIInputModule.enabled = false;
    }

    public static void HideLoadingCircle()
    {
        if (Instance == null)
            return;

        Instance._landscapeUI?.HideLoadingCircle();
        Instance._portraitUI?.HideLoadingCircle();
        
        if (EventSystem.current != null &&
            EventSystem.current.TryGetComponent<InputSystemUIInputModule>(out var inputSystemUIInputModule))
            inputSystemUIInputModule.enabled = true;
    }

    [Button]
    private void Show()
    {
        ShowLoadingCircle();
    }

    [Button]
    private void Hide()
    {
        HideLoadingCircle();
    }
}
