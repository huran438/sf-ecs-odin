using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SFramework.ECS.Runtime;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SFramework.ECS.Editor
{
    internal sealed class SFEcsComponentPreview
    {
        private const int MaxDepth = 4;
        private const int MaxRows = 64;
        private const int MaxCollectionItems = 16;
        private static readonly Dictionary<Type, FieldInfo[]> VisibleFields = new();

        private readonly List<FieldValue> _fields = new();
        private readonly List<FieldInfo> _path = new();
        private bool _expanded = true;

        public Type ComponentType { get; }
        public int FieldCount => _fields.Count;

        public SFEcsComponentPreview(Type componentType) => ComponentType = componentType;

        public void Update(object component)
        {
            _fields.Clear();
            foreach (var field in GetFields(ComponentType))
                CaptureField(field.Name, field, component, 0, writable: true);
        }

        public void Draw(object liveComponent, Action<IReadOnlyList<FieldInfo>, object> edit,
            Action remove, Action cancelInput, bool removesEntity)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var expanded = EditorGUILayout.Foldout(_expanded, new GUIContent(ComponentType.Name, ComponentType.FullName), true);
                if (expanded != _expanded)
                {
                    _expanded = expanded;
                    cancelInput();
                    GUIUtility.ExitGUI();
                }
                var tooltip = removesEntity ? "Removing the last component also deletes the entity." : "Remove this component.";
                using (new EditorGUI.DisabledScope(liveComponent == null))
                    if (GUILayout.Button(new GUIContent("Remove", tooltip), EditorStyles.miniButton, GUILayout.Width(62))) remove();
            }
            if (!_expanded) return;
            if (_fields.Count == 0)
            {
                EditorGUILayout.LabelField("No visible fields", EditorStyles.miniLabel);
                return;
            }

            foreach (var field in _fields)
            {
                if (field.Path == null)
                {
                    EditorGUILayout.LabelField(new GUIContent(field.Label), new GUIContent(field.Text, field.Text));
                    continue;
                }

                using var disabled = new EditorGUI.DisabledScope(liveComponent == null);
                using var changed = new EditorGUI.ChangeCheckScope();
                // Use values from this GUI event so compound widgets preserve the channels the user did not edit.
                var previous = liveComponent == null ? field.Value : ReadField(liveComponent, field.Path);
                var value = SFEcsComponentValueField.Draw(field.Label, field.Path[field.Path.Length - 1].FieldType, previous);
                if (changed.changed && liveComponent != null && !Equals(value, previous)) edit(field.Path, value);
            }
        }

        private static object ReadField(object owner, FieldInfo[] path)
        {
            foreach (var field in path) owner = field.GetValue(owner);
            return owner;
        }

        private void CaptureField(string label, FieldInfo field, object owner, int depth, bool writable)
        {
            if (!CanCaptureMore()) return;
            writable &= !field.IsInitOnly && !field.IsDefined(typeof(ReadOnlyAttribute)) &&
                        !field.IsDefined(typeof(DisableInPlayModeAttribute));
            var value = field.GetValue(owner);
            _path.Add(field);
            if (writable && SFEcsComponentValueField.Supports(field.FieldType))
                _fields.Add(new FieldValue(label, null, _path.ToArray(), value));
            else
                Capture(label, value, depth, writable && field.FieldType.IsValueType);
            _path.RemoveAt(_path.Count - 1);
        }

        private bool CanCaptureMore()
        {
            if (_fields.Count < MaxRows) return true;
            if (_fields.Count == MaxRows)
                _fields.Add(new FieldValue("…", "More fields"));
            return false;
        }

        // Managed object and collection internals remain display-only. Edits only traverse value-type fields.
        private void Capture(string path, object value, int depth, bool writable = false)
        {
            if (!CanCaptureMore()) return;
            if (value == null)
            {
                _fields.Add(new FieldValue(path, "null"));
                return;
            }
            if (value is Object unityObject)
            {
                var label = unityObject == null ? "Missing" : $"{unityObject.name} ({value.GetType().Name})";
                _fields.Add(new FieldValue(path, label));
                return;
            }
            if (value is SFEcsWorld world)
            {
                _fields.Add(new FieldValue(path, world.DebugName));
                return;
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string or decimal or Vector2 or Vector3 or Vector4 or
                Vector2Int or Vector3Int or Quaternion or Color or Color32 or Rect or RectInt or Bounds or BoundsInt)
            {
                _fields.Add(new FieldValue(path, value.ToString()));
                return;
            }
            if (depth == MaxDepth)
            {
                _fields.Add(new FieldValue(path, type.Name));
                return;
            }
            if (value is IList list && (value is Array { Rank: 1 } array && array.GetLowerBound(0) == 0 ||
                type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)))
            {
                _fields.Add(new FieldValue(path, $"{list.Count} items"));
                for (var i = 0; i < Math.Min(list.Count, MaxCollectionItems); i++)
                    Capture($"{path}[{i}]", list[i], depth + 1);
                if (list.Count > MaxCollectionItems) Capture($"{path}[…]", $"{list.Count - MaxCollectionItems} more", depth + 1);
                return;
            }

            var fields = GetFields(type);
            if (fields.Length == 0) _fields.Add(new FieldValue(path, type.Name));
            foreach (var field in fields) CaptureField($"{path}.{field.Name}", field, value, depth + 1, writable);
        }

        private static FieldInfo[] GetFields(Type type)
        {
            if (VisibleFields.TryGetValue(type, out var fields)) return fields;
            fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => !field.IsDefined(typeof(HideInInspector)) &&
                    (field.IsPublic || field.IsDefined(typeof(SerializeField)) || field.IsDefined(typeof(SerializeReference)) ||
                     field.IsDefined(typeof(ShowInInspectorAttribute)) || field.IsDefined(typeof(OdinSerializeAttribute))))
                .ToArray();
            VisibleFields.Add(type, fields);
            return fields;
        }

        private readonly struct FieldValue
        {
            public readonly string Label;
            public readonly string Text;
            public readonly FieldInfo[] Path;
            public readonly object Value;

            public FieldValue(string label, string text, FieldInfo[] path = null, object value = null)
            {
                Label = label;
                Text = text;
                Path = path;
                Value = value;
            }
        }
    }
}
