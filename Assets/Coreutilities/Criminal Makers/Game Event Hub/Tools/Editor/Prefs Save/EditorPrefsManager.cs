using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CriminalMakers.GameEventHub.Tools
{
    public static class EditorPrefsManager
    {
        private static readonly Dictionary<Type, Func<string, object>> DeserializationMethods =
            new Dictionary<Type, Func<string, object>>
            {
                { typeof(string), EditorPrefs.GetString },
                { typeof(int), key => EditorPrefs.GetInt(key) },
                { typeof(float), key => EditorPrefs.GetFloat(key) },
                { typeof(bool), key => EditorPrefs.GetBool(key) }
            };

        private static readonly Dictionary<Type, Action<string, object>> SerializationMethods =
            new Dictionary<Type, Action<string, object>>
            {
                { typeof(string), (key, value) => EditorPrefs.SetString(key, (string)value) },
                { typeof(int), (key, value) => EditorPrefs.SetInt(key, (int)value) },
                { typeof(float), (key, value) => EditorPrefs.SetFloat(key, (float)value) },
                { typeof(bool), (key, value) => EditorPrefs.SetBool(key, (bool)value) }
            };

        public static void Load(object instance)
        {
            _Load(instance, null);
        }

        public static void Load(object instance, Action onDataLoad)
        {
            _Load(instance, onDataLoad);
        }

        public static void _Load(object instance, Action onDataLoad)
        {
            // Find all fields with the EditorPrefProp attribute
            var fields = instance.GetType().GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                var attribute = (EditorPrefProp)Attribute.GetCustomAttribute(field, typeof(EditorPrefProp));
                if (attribute == null)
                {
                    continue;
                }

                // Check if type can be automatically deserialized
                if (DeserializationMethods.ContainsKey(field.FieldType))
                {
                    var value = DeserializationMethods[field.FieldType](attribute.Key);
                    field.SetValue(instance, value);

                    continue;
                }

                // Check if custom deserialization method is provided
                if (attribute.NameOfLoadMethod != null)
                {
                    var loadMethod = instance.GetType().GetMethod(attribute.NameOfLoadMethod,
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                    var value = loadMethod?.Invoke(instance, new object[] { attribute.Key });
                    field.SetValue(instance, value ?? attribute.DefaultValue);

                    continue;
                }

                Debug.LogWarning($"No deserialization method found for field {field.Name}. Skipping.");
            }

            onDataLoad?.Invoke();
        }

        public static void Save(object instance)
        {
            _Save(instance, null);
        }

        public static void Save(object instance, Action onDataSaved)
        {
            _Save(instance, onDataSaved);
        }

        private static void _Save(object instance, Action onDataSaved)
        {
            var fields = instance.GetType().GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                var attribute = (EditorPrefProp)Attribute.GetCustomAttribute(field, typeof(EditorPrefProp));
                if (attribute == null)
                {
                    continue;
                }

                if (SerializationMethods.TryGetValue(field.FieldType, out var serializer))
                {
                    serializer(attribute.Key, field.GetValue(instance));
                    continue;
                }

                if (attribute.NameOfSaveMethod != null)
                {
                    // Call custom serialization method (name of method is provided in attribute)
                    var saveMethod = instance.GetType().GetMethod(attribute.NameOfSaveMethod,
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
                    saveMethod?.Invoke(instance, new[] { attribute.Key, field.GetValue(instance) });
                    continue;
                }

                Debug.LogWarning($"No serialization method found for field {field.Name}. Skipping.");
            }

            onDataSaved?.Invoke();
        }
    }
}