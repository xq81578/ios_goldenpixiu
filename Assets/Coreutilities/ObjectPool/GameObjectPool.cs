using UnityEngine;
using UnityEngine.Pool;

public class GameObjectPool : MonoBehaviour
{
    [SerializeField]
    public GameObject _prefab;
    [SerializeField]
    public int _initSize = 10; // 初始池子大小
    [SerializeField]
    public int _maxSize = 100; // 最大池子大小
    private ObjectPool<GameObject> _objPool;

    private void Awake()
    {
        _objPool = new ObjectPool<GameObject>(
            () =>
            {
                // 物件池創建物件的時候，執行的動作
                GameObject obj = Instantiate(_prefab);
                obj.transform.SetParent(transform);
                obj.SetActive(false);
                return obj;
            },
            (obj) =>
            {
                // 物件池拿取物件的時候，執行的動作
            },
            (obj) =>
            {
                // 物件池回收物件的時候，執行的動作
                obj.SetActive(false);
            },
            (obj) =>
            {
                // 物件池銷毀物件的時候，執行的動作
                Destroy(obj);
            },
            true,
            _initSize,
            _maxSize
        );
    }

    // 從池子中獲取物件
    public GameObject GetObject(bool isActive = true, Transform parent = null)
    {
        GameObject obj = _objPool.Get();
        if (isActive)
        {
            obj.SetActive(isActive);
        }
        if (parent != null)
        {
            obj.transform.SetParent(parent);
        }
        return obj;
    }

    // 將物件返回到池子中
    public void ReturnObject(GameObject obj)
    {
        if (obj == null)
            return;
        _objPool.Release(obj);
    }

    // 重置池子
    public void ResetPool()
    {
        _objPool.Clear();
    }

    // 返回池子的大小
    public int GetPoolCount()
    {
        return _objPool.CountAll;
    }

    // 返回當前池子的使用數量
    public int GetCurrentSize()
    {
        return _objPool.CountActive;
    }

    // 返回當前池子的剩餘大小
    public int GetRemainSize()
    {
        return _objPool.CountInactive;
    }
}
