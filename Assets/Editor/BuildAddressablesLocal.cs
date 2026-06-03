#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

public static class BuildAddressablesLocal
{
    public static void Build()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            throw new System.InvalidOperationException("AddressableAssetSettings not found.");
        }

        AddressableAssetSettings.CleanPlayerContent();
        AddressableAssetSettings.BuildPlayerContent();
        AssetDatabase.SaveAssets();
    }
}
#endif
