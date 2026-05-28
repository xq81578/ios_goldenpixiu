using System.IO;
using CriminalMakers.GameEventHub.Utilities;
using UnityEditor;
using UnityEngine;

namespace CriminalMakers.GameEventHub.EditorAddon
{
    public class GameEventHubRightClickActions
    {
        [MenuItem("Assets/Create/Game Event Hub/Game Event ScriptableObject", false, 51)]
        public static void CreateGameEventSo()
        {
            // Path to save the ScriptableObject in the current selection
            string path = GetSelectedPathOrFallback();
            string assetName = "Game Event SO.asset";

            // Create a new instance of the ScriptableObject
            var asset = ScriptableObject.CreateInstance<GameEventSO>();

            // Generate a unique path for the asset to avoid conflicts
            path = AssetDatabase.GenerateUniqueAssetPath($"{path}/{assetName}");

            // Save the asset in the Asset Database
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            // Focus on the newly created asset in the Project view
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }
        
        
        private static string GetSelectedPathOrFallback()
        {
            string path = "Assets";

            // Loop through currently selected objects in the Project view
            foreach (Object obj in Selection.GetFiltered(typeof(Object), SelectionMode.Assets))
            {
                path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && File.Exists(path) == false) // Ensure it's a folder
                {
                    return path;
                }
            }
            return path;
        }
    }
}