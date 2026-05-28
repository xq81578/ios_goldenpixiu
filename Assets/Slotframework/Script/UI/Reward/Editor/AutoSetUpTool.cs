#if UNITY_EDITOR
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

public static class AutoSetUpTool
{
    public static void SetUpSpine(SkeletonGraphic spin, params string[] keywords)
    {
        if (spin == null)
            return;

        for (int i = 0; i < keywords.Length; i++)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"{keywords[i]} t:SkeletonDataAsset");

            if (guids.Length == 0)
            {
                continue;
            }

            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<SkeletonDataAsset>(path);

            if (asset != null)
            {
                spin.skeletonDataAsset = asset;
                spin.Initialize(true);
                break;
            }
        }
    }

    public static void SetUpImage(Transform transform, string objectName, params string[] keywords)
    {
        Transform t = transform.Find(objectName);
        Image image = t != null ? t.GetComponent<Image>() : null;
        SetUpImage(image, keywords);
    }

    public static void SetUpImage(Image image, params string[] keywords)
    {
        if (image == null)
            return;

        for (int i = 0; i < keywords.Length; i++)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets(keywords[i] + " t:Sprite");

            if (guids.Length == 0)
            {
                continue;
            }

            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (asset != null)
            {
                image.sprite = asset;
                image.SetNativeSize();
                break;
            }
        }
    }

    public static (string, string) GetSetUpSpineInLoopString(SkeletonGraphic spin)
    {
        (string, string) result = ("in", "loop");
        if (spin == null || spin.SkeletonDataAsset == null)
            return result;

        // 找出 skeletonDataAsset 的所有動畫名稱
        var skeletonData = spin.SkeletonDataAsset.GetSkeletonData(true);
        foreach (var animation in skeletonData.Animations)
        {
            string animationName = animation.Name;
            if (animationName.Contains("in") && !animationName.Contains("Win"))
            {
                result.Item1 = animationName;
            }
            else if (animationName.Contains("loop"))
            {
                result.Item2 = animationName;
            }
        }

        return result;
    }

    public static void ClearImage(Transform transform, string objectName)
    {
        Transform t = transform.Find(objectName);
        Image image = t != null ? t.GetComponent<Image>() : null;
        if (image != null)
        {
            image.sprite = null;
        }
    }
}
#endif