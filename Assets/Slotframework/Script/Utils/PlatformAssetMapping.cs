using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Slot.Common;

namespace Slot.Common
{
    /// <summary>
    /// 根據不同平台類型對應不同的 AssetReference
    /// </summary>
    [System.Serializable]
    public class PlatformAssetMapping
    {
        [System.Serializable]
        public class PlatformAssetEntry
        {
            public PlatformType PlatformType;
            public AssetReference AssetReference;
            public GameObject gameObject;
        }

        public List<PlatformAssetEntry> PlatformAssets = new List<PlatformAssetEntry>();

        /// <summary>
        /// 根據平台類型取得對應的 GameObject
        /// </summary>
        public async UniTask<GameObject> GetGameObject(PlatformType platformType, Transform parent)
        {
            var entry = PlatformAssets.Find(x => x.PlatformType == platformType);
            if (entry == null)
                return null;

            if (entry.gameObject != null)
                return entry.gameObject;

            if (entry.AssetReference == null || !entry.AssetReference.RuntimeKeyIsValid())
                return null;

            var handle = entry.AssetReference.LoadAssetAsync<GameObject>();

            await handle.ToUniTask();

            if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            {
                GameObject prefab = handle.Result;
                if (prefab == null)
                    return null;

                GameObject instantiatedObject = Object.Instantiate(prefab, parent);
                instantiatedObject.name = prefab.name;
                entry.gameObject = instantiatedObject;
                return instantiatedObject;
            }

            return null;
        }
    }
}