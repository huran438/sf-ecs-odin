using System;
using System.Linq;
using SFramework.ECS.Runtime;
using UnityEditor;
using UnityEngine;

namespace SFramework.ECS.Editor
{
    [CustomPropertyDrawer(typeof(SFSerializableType))]
    public class SFSerializableTypeDrawer : PropertyDrawer {
        SFSerializableTypeFilterAttribute _serializableTypeFilter;
        string[] typeNames, typeFullNames;

        void Initialize() {
            if (typeFullNames != null) return;
            
            _serializableTypeFilter = (SFSerializableTypeFilterAttribute) Attribute.GetCustomAttribute(fieldInfo, typeof(SFSerializableTypeFilterAttribute));
            
            var filteredTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => _serializableTypeFilter == null ? DefaultFilter(t) : _serializableTypeFilter.Filter(t))
                .ToArray();
            
            typeNames = filteredTypes.Select(t => t.ReflectedType == null ? t.Name : $"{t.ReflectedType.Name} + {t.Name}").ToArray();
            typeFullNames = filteredTypes.Select(t => t.AssemblyQualifiedName).ToArray();
        }
        
        static bool DefaultFilter(Type type) {
            return !type.IsAbstract && !type.IsInterface && !type.IsGenericType;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            Initialize();
            var typeIdProperty = property.FindPropertyRelative("assemblyQualifiedName");

            if (string.IsNullOrEmpty(typeIdProperty.stringValue)) {
                typeIdProperty.stringValue = typeFullNames.First();
                property.serializedObject.ApplyModifiedProperties();
            }

            var currentIndex = Array.IndexOf(typeFullNames, typeIdProperty.stringValue);
            var selectedIndex = EditorGUI.Popup(position, label.text, currentIndex, typeNames);
            
            if (selectedIndex >= 0 && selectedIndex != currentIndex) {
                typeIdProperty.stringValue = typeFullNames[selectedIndex];
                property.serializedObject.ApplyModifiedProperties();
            }
        }
    }
}