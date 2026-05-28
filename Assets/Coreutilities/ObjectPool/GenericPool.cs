using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 泛型物件池，支援任意繼承 Component 的類型 T
/// </summary>
public class GenericPool<T> : MonoBehaviour where T : Component
{
    [SerializeField]
    private T _prefab;              // 預置體，必須帶有 T
    [SerializeField]
    private int _initSize = 10;     // 初始池子大小
    [SerializeField]
    private int _maxSize = 100;     // 最大池子大小

    private ObjectPool<T> _objPool; // UnityEngine.Pool 提供的通用物件池

    private void Awake()
    {
        // 建立 ObjectPool<T>
        _objPool = new ObjectPool<T>(
            // 建立時
            () =>
            {
                T component = Instantiate(_prefab, transform);
                component.gameObject.SetActive(false);
                return component;
            },
            // 取出時
            component =>
            {
                // 可以做一些復位或初始化的動作
            },
            // 回收時
            component =>
            {
                component.gameObject.SetActive(false);
                component.transform.SetParent(transform);
            },
            // 銷毀時
            component =>
            {
                Destroy(component.gameObject);
            },
            // CollectionCheck: 是否檢查重複回收
            true,
            // 預設容量
            _initSize,
            // 最大容量
            _maxSize
        );
    }

    /// <summary>
    /// 從池子中獲取 T，並可選擇是否啟用、指定父物件
    /// </summary>
    public T GetObject(bool isActive = true, Transform parent = null)
    {
        T component = _objPool.Get();
        if (parent != null)
        {
            component.transform.SetParent(parent);
        }
        if (isActive)
        {
            component.gameObject.SetActive(true);
        }
        return component;
    }

    /// <summary>
    /// 將物件回收至池子
    /// </summary>
    public void ReturnObject(T component)
    {
        if (component == null)
            return;
        _objPool.Release(component);
    }

    /// <summary>
    /// 重置物件池，清除所有已產生的物件
    /// </summary>
    public void ResetPool()
    {
        _objPool.Clear();
    }

    /// <summary>
    /// 取得池子的總數量(包含使用中與未使用)
    /// </summary>
    public int GetPoolCount()
    {
        return _objPool.CountAll;
    }

    /// <summary>
    /// 取得目前使用中的物件數量
    /// </summary>
    public int GetCurrentSize()
    {
        return _objPool.CountActive;
    }

    /// <summary>
    /// 取得目前池子剩餘可用的物件數量
    /// </summary>
    public int GetRemainSize()
    {
        return _objPool.CountInactive;
    }
}
