using System.Collections;
using System.Collections.Generic;
using CriminalMakers.GameEventHub;
using UnityEditor;
using UnityEngine;

namespace CriminalMakers.GameEventHub.EditorAddon
{
    public class CreateGameEventHub : MonoBehaviour
    {
        [MenuItem("GameObject/Game Event Hub", false, 10)]
        private static void CreateCustomObject(MenuCommand menuCommand)
        {
            if (GameEventHub.IsInitialized)
            {
                Debug.LogWarning("Game Event Hub already exists in the scene. Refusing to create another one.");
                return;
            }

            var go = GameEventHub.CreateOrRetrieveInstance().gameObject;

            // Ensure the new object is parented to the currently selected one in the hierarchy
            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);

            // Register the creation in Undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);

            // Select the newly created GameObject
            Selection.activeObject = go;
        }
    }
}