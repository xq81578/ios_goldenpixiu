using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class AddressableImageLoader : MonoBehaviour
{
    public AssetReference spriteReference;

    private Image image;

    private async void Start()
    {
        image = GetComponent<Image>();
        image.sprite = null;

        var sprite = await SourceManager.Load<Sprite>(spriteReference);

        if (sprite != null)
        {
            LogUtils.Log($"[AddressableImageLoader] sprite:{sprite.name} 載入成功！");
            image.sprite = sprite;
        }
        else
        {
            LogUtils.LogError($"[AddressableImageLoader] 載入圖片失敗: {spriteReference}");
        }
    }
}
