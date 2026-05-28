using System;
using System.IO;
using System.Linq;
using CriminalMakers.GameEventHub.Utilities;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;

namespace CriminalMakers.GameEventHub
{
    public static class GameEventsHelper
    {
        public static string TestToolName = "Tester Tool";
        
        public static bool IsObjectUnityNull(object obj)
        {
            return obj == null || obj.Equals(null);
        }

        public static bool IsComponent(object obj)
        {
            return !IsObjectUnityNull(obj) && obj is Component;
        }
        
        public static bool IsAnimator(object obj)
        {
            return !IsObjectUnityNull(obj) && obj is Animator;
        }
        
        public static bool IsScriptableObject(object obj)
        {
            return !IsObjectUnityNull(obj) && obj is ScriptableObject;
        }
        
        public static bool IsRectTransform(object obj)
        {
            if(IsObjectUnityNull(obj) || !IsComponent(obj)) return false;
                
            var component = obj as Component;    
            return component?.GetComponent<RectTransform>() != null;
        }

        

        public static bool EmmitterIsTesterTool(object obj)
        {
            return obj.ToString() == TestToolName;
        }

        public static bool IsSystemEvent(Type evenType)
        {
            try
            {
                return evenType.GetCustomAttributes(typeof(SystemEventAttribute), true).Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string GetEventBindingKey(GameEvent gameEvent)
        {
            return gameEvent.GetType().AssemblyQualifiedName;
        }

        public static string GetEventBindingKey<TEvent>() where TEvent : GameEvent
        {
            return typeof(TEvent).AssemblyQualifiedName;
        }

        public static string GetEventBindingKey(Type eventType)
        {
            if (eventType == null || !typeof(GameEvent).IsAssignableFrom(eventType))
            {
                throw new ArgumentException("Type must be a subclass of GameEvent.", nameof(eventType));
            }

            return eventType.AssemblyQualifiedName;
        }

        public static string GetEventName<TEvent>() where TEvent : GameEvent
        {
            return typeof(TEvent).Name;
        }

        public static string GetEventName(Type eventType)
        {
            if (eventType == null || !typeof(GameEvent).IsAssignableFrom(eventType))
            {
                throw new ArgumentException("Type must be a subclass of GameEvent. Received: " + eventType,
                    nameof(eventType));
            }

            return eventType.Name;
        }

        public static SubscriberPriority GetBindingPriority(object binding)
        {
            if (binding is GameEventHub.StaticSubscriberBindingInfo staticBindings)
            {
                return staticBindings.Priority;
            }

            if (binding is GameEventHub.DynamicSubscriberBindingInfo<GameEvent> dynamicBindings)
            {
                return dynamicBindings.Priority;
            }

            return SubscriberPriority.Medium;
        }

#if UNITY_EDITOR
        
        public static GameEventSO lastCreatedGameEvent;
        
        public static bool SaveGameEventAsScriptableObject(GameEvent gameEvent)
        {
            var defaultName = gameEvent?.GetType().Name ?? "TemporalGameEventWrapper";
            var path = EditorUtility.SaveFilePanelInProject("Save Game Event SO", defaultName, "asset",
                "Save Game Event SO");

            if (string.IsNullOrEmpty(path)) return false;
            
            var gameEventSo = ScriptableObject.CreateInstance<GameEventSO>();
            gameEventSo.Initialize(gameEvent);
                    
            AssetDatabase.CreateAsset(gameEventSo, path);
            AssetDatabase.SaveAssets();
            
            lastCreatedGameEvent = gameEventSo;
            
            return true;
        }
        
        public static bool PingGameobject(object sceneObject)
        {
            var component = sceneObject as Component;
            if (component == null)
            {
                return false;
            }

            Selection.activeGameObject = component.gameObject;
            EditorGUIUtility.PingObject(component.gameObject);
            return true;
        }

        public static bool PingScriptableObject(object scriptableObject)
        {
            var asset = scriptableObject as ScriptableObject;
            if (asset == null)
            {
                return false;
            }
            
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return true;
        }
        
        public static void OpenScript(Type type)
        {
            // Get all MonoScripts in the project
            var allScripts = MonoImporter.GetAllRuntimeMonoScripts();

            // Find the MonoScript matching the specified type
            foreach (var script in allScripts)
            {
                if (script.GetClass() == type) // Match the type with the script's class
                {
                    AssetDatabase.OpenAsset(script); // Open the script in Unity Editor
                    return;
                }
            }

            Debug.LogError($"Script for type '{type.FullName}' not found in the project.");
        }

        public static void OpenDemoScene()
        {
            EditorSceneManager.OpenScene("Assets/Criminal Makers/Game Event Hub/Demo/GameEventHubDemo.unity");
        }
        
        public static bool DemoSceneExists()
        {
            // check if file exists
            return File.Exists("Assets/Criminal Makers/Game Event Hub/Demo/GameEventHubDemo.unity");
        }
        
        public static Type FindDocumentation()
        {
            var documentationType = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(t => t.FullName == "CriminalMakers.GameEventHub.Documentation.GameEventHubDocumentation");
            
            return documentationType;
        }

        public static bool DocumentationExists()
        {
            return FindDocumentation() != null;
        }
        
        public static void SafetlyOpenDocumentation(string sectionName = null)
        {
            if (DocumentationExists())
            {
                var documentation = FindDocumentation();
                if (sectionName != null)
                {
                    var method = documentation.GetMethod("ShowWindow", new Type[] { typeof(string) });
                    method?.Invoke(null, new object[] { sectionName });
                }
                else
                {
                    var method = documentation.GetMethod("ShowWindow", new Type[] { });
                    method?.Invoke(null, new object[] { });
                }
            }
        }
        
#endif
    }
}