using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CriminalMakers.GameEventHub.Utilities
{
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorPropertyDrawer : PropertyDrawer
    {
        #region Overrides

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // Get attribute SubclassSelectorAttribute
            SubclassSelectorAttribute attribute = fieldInfo.GetCustomAttribute<SubclassSelectorAttribute>();
            
            var visualElement = new VisualElement();

            var propertyField = new PropertyField();
            propertyField.BindProperty(property);
            propertyField.label = property.displayName;

            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                propertyField.label = property.displayName;
                visualElement.Add(propertyField);
                return visualElement;
            }

            var types = GetTypes(fieldInfo, property, attribute.includeExcludedByAttribute);

            var dropdownField = new TypePopupField(property, types);
            visualElement.Add(dropdownField);

            if (attribute.drawProperties)
            {
                RenderInlineFields(property, visualElement);
            }

            return visualElement;
        }

        private void RenderInlineFields(SerializedProperty property, VisualElement parent)
        {
            if (property.managedReferenceValue == null)
            {
                return;
            }
            
            var containerWithBackground = new Box();
            containerWithBackground.style.borderTopWidth = 1;
            containerWithBackground.style.borderBottomWidth = 1;
            containerWithBackground.style.borderLeftWidth = 1;
            containerWithBackground.style.borderRightWidth = 1;
            containerWithBackground.style.borderBottomColor = new StyleColor(Color.gray);
            containerWithBackground.style.borderTopColor = new StyleColor(Color.gray);
            containerWithBackground.style.borderLeftColor = new StyleColor(Color.gray);
            containerWithBackground.style.borderRightColor = new StyleColor(Color.gray);
            containerWithBackground.style.marginTop = 5;
            containerWithBackground.style.marginBottom = 5;
            containerWithBackground.style.paddingTop = 5;
            containerWithBackground.style.paddingBottom = 5;
            containerWithBackground.style.paddingLeft = 10;
            containerWithBackground.style.paddingRight = 5;

            // Iterate through the visible child properties (inline rendering)
            var iterator = property.Copy();
            var endProperty = iterator.GetEndProperty();

            // Expand properties inline until we reach the end
            iterator.NextVisible(true); // Move to the first child
            bool hasFields = false;
            while (!SerializedProperty.EqualContents(iterator, endProperty))
            {
                hasFields = true;
                // Create a PropertyField for each child and add it inline
                var childPropertyField = new PropertyField(iterator);
                childPropertyField.BindProperty(iterator);
                childPropertyField.label = ObjectNames.NicifyVariableName(iterator.name);
                
                containerWithBackground.Add(childPropertyField);

                // parent.Add(childPropertyField);
                iterator.NextVisible(false); // Move to the next property
            }
            
            if (!hasFields)
            {
                var noFieldsLabel = new Label("No fields to display");
                noFieldsLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                containerWithBackground.Add(noFieldsLabel);
            }
            
            parent.Add(containerWithBackground);
        }

        #endregion

        #region Internal Methods

        private static bool IsCollection(Type fieldType)
        {
            if (fieldType.IsArray)
            {
                return true;
            }

            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return true;
            }

            return false;
        }

        private static List<Type> GetTypes(FieldInfo fieldInfo, SerializedProperty property, bool includeExcludedByAttribute = false)
        {
            var value = property.managedReferenceValue;
            Type currentType = value?.GetType();

            Type fieldType = fieldInfo.FieldType;
            Type baseType;

            bool isCollection = IsCollection(fieldType);

            var types = new List<Type>()
            {
                currentType,
                null
            };

            if (fieldType.IsAbstract == false && isCollection == false)
            {
                types.Add(fieldType);
            }

            if (isCollection)
            {
                baseType = fieldType.GetGenericArguments()[0];
                if (baseType.IsAbstract == false)
                {
                    types.Add(baseType);
                }
            }
            else
            {
                baseType = fieldType;
            }

            var derivedTypes = TypeCache.GetTypesDerivedFrom(baseType);
            foreach (var derivedType in derivedTypes)
            {
                // get ExcludeSubclassSelectorAttribute attribute in type
                var excludeAttribute = derivedType.GetCustomAttribute<ExcludeSubclassSelectorAttribute>();
                if (excludeAttribute != null && includeExcludedByAttribute == false)
                {
                    continue;
                }
                types.Add(derivedType);
            }

            return types;
        }

        #endregion

        #region Internal Types

        public sealed class TypePopupField : PopupField<Type>
        {
            #region Internal Members

            private readonly SerializedProperty _property;

            #endregion

            public TypePopupField(SerializedProperty property, List<Type> types) : base(property.displayName, types, 0,
                GetTypeName, GetTypeName)
            {
                _property = property;
                this.RegisterValueChangedCallback(OnValueSelected);
            }

            #region Internal Methods

            private void OnValueSelected(ChangeEvent<Type> changeEvent)
            {
                Type selectedType = changeEvent.newValue;
                if (selectedType == null)
                {
                    _property.managedReferenceValue = null;
                    _property.serializedObject.ApplyModifiedProperties();
                }
                else
                {
                    var constructor = selectedType.GetConstructor(Type.EmptyTypes);
                    if (constructor != null)
                    {
                        var value = constructor.Invoke(null);
                        _property.managedReferenceValue = value;
                        _property.serializedObject.ApplyModifiedProperties();
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"Selected Type {selectedType.Name} does not have a parameterless constructor. Cannot assign instance of type.");
                    }
                }
            }

            private static string GetTypeName(Type type)
            {
                if (type == null)
                {
                    return "<No Instance>";
                }
                else
                {
                    var constructor = type.GetConstructor(Type.EmptyTypes);
                    return constructor == null ? $"(No default constructor!) {ObjectNames.NicifyVariableName(type.Name)}" : ObjectNames.NicifyVariableName(type.Name);
                }
            }

            #endregion
        }

        #endregion
    }
}