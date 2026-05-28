using UnityEngine;

/// <summary>
/// 直橫版型管理
/// </summary>
public class ResolutionManager : Singleton<ResolutionManager>
{
    /// <summary>
    /// 橫版物件
    /// </summary>
    [SerializeField]
    protected Canvas LandscapeUI;

    /// <summary>
    /// 直版物件
    /// </summary>
    [SerializeField]
    protected Canvas PortraitUI;

    [SerializeField]
    protected Camera CustomCamera;

    //是否為橫版
    protected bool isLandscape;
    //目前版型是否為橫版
    protected bool currentIsLandscape;

    private void Start()
    {
        Init();
    }

    private void Update()
    {
        isLandscape = CheckLandscape();
        if (currentIsLandscape != isLandscape)
        {
            UpdateCanvas();
        }
    }

    //確認現在有幾種版型
    private int GetResolutionCount()
    {
        if (LandscapeUI == null && PortraitUI == null)
        {
            return 0;
        }
        else if ((LandscapeUI != null && PortraitUI == null) ||
                (LandscapeUI == null && PortraitUI != null))
        {
            return 1;
        }

        return 2;
    }

    public virtual void Init()
    {
        if (GetResolutionCount() > 1)
        {
            LandscapeUI.gameObject.SetActive(true);
            PortraitUI.gameObject.SetActive(true);
        }

        isLandscape = CheckLandscape();
        currentIsLandscape = isLandscape;
        SetCanvas();

    }

    public virtual void UpdateCanvas()
    {
        currentIsLandscape = isLandscape;
        SetCanvas();

    }

    //檢查是否為橫版
    //16:9 1.7
    //9:16 0.56
    private bool CheckLandscape()
    {
        //高 > 寬就切成直版
        //如果沒有自定義相機就用主相機
        if (CustomCamera != null)
        {
            return CustomCamera.aspect >= 1;
        }
        else
        {
            return Camera.main.aspect >= 1;
        }
    }

    //設定要顯示的版型
    private void SetCanvas()
    {
        if (GetResolutionCount() < 2) return;

        if (currentIsLandscape)
        {
            LandscapeUI.planeDistance = 100;
            PortraitUI.planeDistance = -200;
        }
        else
        {
            LandscapeUI.planeDistance = -200;
            PortraitUI.planeDistance = 100;
        }
    }

    //檢查對應版型是否開啟中
    public virtual bool CheckIsOn(Transform trans)
    {
        if (GetResolutionCount() < 2) return true;

        if (LandscapeUI == null)
        {
            LogUtils.LogWarning("[ResolutionManager] LandscapeUI is Null");
            return (isLandscape && trans == LandscapeUI.transform) ||
                    (!isLandscape && trans != LandscapeUI.transform);
        }

        return (isLandscape && trans.root == LandscapeUI.transform) ||
                (!isLandscape && trans.root == PortraitUI.transform);
    }

    //檢查物件是否為橫版
    public bool CheckRootIsLandscape(Transform trans)
    {
        if (LandscapeUI == null)
        {
            LogUtils.LogWarning("[ResolutionManager] LandscapeUI is Null");
            return trans == LandscapeUI.transform;
        }

        return trans.root == LandscapeUI.transform;
    }
}
