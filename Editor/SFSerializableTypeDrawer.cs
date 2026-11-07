using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            
            _serializableTypeFilter = fieldInfo?.GetCustomAttribute<SFSerializableTypeFilterAttribute>();
            
            var filteredTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(t => _serializableTypeFilter == null ? DefaultFilter(t) : _serializableTypeFilter.Filter(t))
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .ToArray();

            typeNames = new[] { "None" }.Concat(filteredTypes.Select(t => t.FullName)).ToArray();
            typeFullNames = new[] { string.Empty }.Concat(filteredTypes.Select(t => t.AssemblyQualifiedName)).ToArray();
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { return exception.Types.Where(type => type != null); }
        }
        
        static bool DefaultFilter(Type type) {
            return !type.IsAbstract && !type.IsInterface && !type.IsGenericType;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Initialize();
            var typeIdProperty = property.FindPropertyRelative("assemblyQualifiedName");
            if (typeIdProperty == null) return;

            var currentValue = typeIdProperty.stringValue ?? string.Empty;
            var currentIndex = Array.IndexOf(typeFullNames, currentValue);
            var options = typeNames;
            if (currentIndex < 0)
            {
                currentIndex = typeNames.Length;
                options = typeNames.Concat(new[] { $"Missing: {currentValue}" }).ToArray();
            }

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var previousMixedValue = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = typeIdProperty.hasMultipleDifferentValues;
                try
                {
                    using var changeCheck = new EditorGUI.ChangeCheckScope();
                    var selectedIndex = EditorGUI.Popup(position, label.text, currentIndex, options);
                    if (changeCheck.changed && selectedIndex >= 0 && selectedIndex < typeFullNames.Length)
                        typeIdProperty.stringValue = typeFullNames[selectedIndex];
                }
                finally { EditorGUI.showMixedValue = previousMixedValue; }
            }
        }
    }
}
