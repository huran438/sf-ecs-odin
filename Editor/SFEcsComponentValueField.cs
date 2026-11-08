using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SFramework.ECS.Editor
{
    internal static class SFEcsComponentValueField
    {
        private static readonly HashSet<Type> Types = new()
        {
            typeof(bool), typeof(string), typeof(char), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
            typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Vector2Int), typeof(Vector3Int),
            typeof(Quaternion), typeof(Color), typeof(Color32), typeof(Rect), typeof(RectInt),
            typeof(Bounds), typeof(BoundsInt)
        };

        internal static bool Supports(Type type) =>
            Types.Contains(type) || type.IsEnum || typeof(Object).IsAssignableFrom(type);

        internal static object Draw(string label, Type type, object value)
        {
            if (typeof(Object).IsAssignableFrom(type))
                return EditorGUILayout.ObjectField(label, (Object)value, type, allowSceneObjects: true);

            switch (value)
            {
                case bool flag: return EditorGUILayout.Toggle(label, flag);
                case int number: return EditorGUILayout.DelayedIntField(label, number);
                case long number: return EditorGUILayout.LongField(label, number);
                case float number: return EditorGUILayout.DelayedFloatField(label, number);
                case double number: return EditorGUILayout.DelayedDoubleField(label, number);
                case byte number: return DrawInteger(label, type, number, byte.MinValue, byte.MaxValue);
                case sbyte number: return DrawInteger(label, type, number, sbyte.MinValue, sbyte.MaxValue);
                case short number: return DrawInteger(label, type, number, short.MinValue, short.MaxValue);
                case ushort number: return DrawInteger(label, type, number, ushort.MinValue, ushort.MaxValue);
                case uint number: return DrawInteger(label, type, number, uint.MinValue, uint.MaxValue);
                case ulong number:
                    return ulong.TryParse(EditorGUILayout.DelayedTextField(label, number.ToString()), out var unsigned)
                        ? unsigned : number;
                case decimal number:
                    return decimal.TryParse(EditorGUILayout.DelayedTextField(label, number.ToString()), out var exact)
                        ? exact : number;
                case char character:
                    var text = EditorGUILayout.DelayedTextField(label, character.ToString());
                    return text.Length == 1 ? text[0] : character;
                case Enum choice:
                    if (!type.IsDefined(typeof(FlagsAttribute), inherit: false)) return EditorGUILayout.EnumPopup(label, choice);
                    var flags = EditorGUILayout.DelayedTextField(label, choice.ToString());
                    return Enum.TryParse(type, flags, ignoreCase: true, out var parsed) ? parsed : choice;
                case Vector2 vector: return EditorGUILayout.Vector2Field(label, vector);
                case Vector3 vector: return EditorGUILayout.Vector3Field(label, vector);
                case Vector4 vector: return EditorGUILayout.Vector4Field(label, vector);
                case Vector2Int vector: return EditorGUILayout.Vector2IntField(label, vector);
                case Vector3Int vector: return EditorGUILayout.Vector3IntField(label, vector);
                case Quaternion rotation:
                    var edited = EditorGUILayout.Vector4Field(label, new Vector4(rotation.x, rotation.y, rotation.z, rotation.w));
                    return new Quaternion(edited.x, edited.y, edited.z, edited.w);
                case Color color: return EditorGUILayout.ColorField(label, color);
                case Color32 color: return (Color32)EditorGUILayout.ColorField(label, color);
                case Rect rect: return EditorGUILayout.RectField(label, rect);
                case RectInt rect: return EditorGUILayout.RectIntField(label, rect);
                case Bounds bounds: return EditorGUILayout.BoundsField(label, bounds);
                case BoundsInt bounds: return EditorGUILayout.BoundsIntField(label, bounds);
                default: return EditorGUILayout.DelayedTextField(label, (string)value ?? "");
            }
        }

        private static object DrawInteger(string label, Type type, long value, long min, long max)
        {
            var number = EditorGUILayout.LongField(label, value);
            return Convert.ChangeType(Math.Max(min, Math.Min(max, number)), type);
        }
    }
}
