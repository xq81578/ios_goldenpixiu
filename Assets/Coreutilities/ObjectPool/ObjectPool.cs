using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    public GameObject Prefab;
    public int InitSize = 10; // 初始池子大小
    public int MaxSize = 100; // 最大池子大小
    private Queue<GameObject> objPool = new Queue<GameObject>();
    private int currentSize;

    void Awake()
    {
        // 初始化物件池
        for (int i = 0; i < InitSize; i++)
        {
            AddObjectToPool();
        }
    }

    // 從池子中獲取物件
    public GameObject GetObject(bool isActive = true)
    {
        GameObject obj;
        if (objPool.Count > 0)
        {
            obj = objPool.Dequeue();
        }
        else if (currentSize < MaxSize)
        {
            // 池子還沒滿時，可以動態擴容
            AddObjectToPool();
            obj = objPool.Dequeue();
        }
        else
        {
            // 池子已滿，無法擴容
            LogUtils.LogWarning("Object Pool is full, cannot create more objects.");
            return null;
        }
        obj.SetActive(isActive);
        return obj;
    }

    // 將物件返回到池子中
    public void ReturnObject(GameObject obj)
    {
        if (obj == null)
            return;
        obj.SetActive(false);
        objPool.Enqueue(obj);
    }

    // 新增物件到池子中
    private GameObject AddObjectToPool()
    {
        GameObject obj = Instantiate(Prefab);
        obj.transform.SetParent(transform);
        obj.SetActive(false);
        objPool.Enqueue(obj);
        currentSize++;
        return obj;
    }

    // 重設池子
    public void ResetPool()
    {
        while (objPool.Count > 0)
        {
            GameObject obj = objPool.Dequeue();
            Destroy(obj);
        }
        currentSize = 0;
        for (int i = 0; i < InitSize; i++)
        {
            AddObjectToPool();
        }
    }

    // 返回當前池子的物件數量
    public int GetPoolCount()
    {
        return objPool.Count;
    }

    // 返回當前池子的總大小
    public int GetCurrentSize()
    {
        return currentSize;
    }
}
