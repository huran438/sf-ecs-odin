using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using SFramework.ECS.Runtime;
using UnityEditor;

[assembly: InternalsVisibleTo("SFramework.ECS.Editor.Tests")]

namespace SFramework.ECS.Editor
{
    internal static class SFEcsComponentEditing
    {
        private static readonly Dictionary<Type, Func<SFEcsWorld, int, bool>> AddOperations = new();
        private static readonly MethodInfo AddMethod =
            typeof(SFEcsComponentEditing).GetMethod(nameof(Add), BindingFlags.Static | BindingFlags.NonPublic);

        internal static Type[] GetAddableTypes(SFEcsWorld world, SFEcsPackedEntity handle)
        {
            if (!handle.Unpack(world, out var entity)) return Array.Empty<Type>();

            ISFEcsPool[] pools = null;
            var count = world.GetAllPools(ref pools);
            var types = new HashSet<Type>(TypeCache.GetTypesDerivedFrom<ISFComponent>());
            types.UnionWith(TypeCache.GetTypesWithAttribute<SFGenerateComponentAttribute>());
            for (var i = 0; i < count; i++) types.Add(pools[i].GetComponentType());

            return types.Where(type => IsPoolType(type) && world.GetPoolByType(type)?.Has(entity) != true)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
        }

        internal static bool TryAdd(SFEcsWorld world, SFEcsPackedEntity handle, Type type)
        {
            if (!handle.Unpack(world, out var entity) || !IsPoolType(type)) return false;
            if (!AddOperations.TryGetValue(type, out var add))
            {
                add = (Func<SFEcsWorld, int, bool>)AddMethod.MakeGenericMethod(type)
                    .CreateDelegate(typeof(Func<SFEcsWorld, int, bool>));
                AddOperations.Add(type, add);
            }
            return add(world, entity);
        }

        internal static bool TryRemove(SFEcsWorld world, SFEcsPackedEntity handle, Type type)
        {
            if (!TryGetPool(world, handle, type, out var entity, out var pool)) return false;
            pool.Del(entity);
            return true;
        }

        internal static object GetCurrentValue(SFEcsWorld world, SFEcsPackedEntity handle, Type type)
        {
            return TryGetPool(world, handle, type, out var entity, out var pool) ? pool.GetRaw(entity) : null;
        }

        internal static bool TrySetField(SFEcsWorld world, SFEcsPackedEntity handle, Type type,
            IReadOnlyList<FieldInfo> path, object value)
        {
            if (path.Count == 0 || !TryGetPool(world, handle, type, out var entity, out var pool)) return false;

            // Merge the edited leaf into the current component. Systems can update its other fields between draws.
            var component = pool.GetRaw(entity);
            if (!TryWriteField(component, path, 0, value)) return false;
            pool.SetRaw(entity, component);
            return true;
        }

        private static bool TryGetPool(SFEcsWorld world, SFEcsPackedEntity handle, Type type, out int entity,
            out ISFEcsPool pool)
        {
            pool = null;
            if (!handle.Unpack(world, out entity)) return false;
            pool = world.GetPoolByType(type);
            return pool?.Has(entity) == true;
        }

        private static bool TryWriteField(object owner, IReadOnlyList<FieldInfo> path, int index, object value)
        {
            var field = path[index];
            if (field == null || field.IsStatic || field.IsInitOnly || !field.DeclaringType.IsInstanceOfType(owner))
                return false;

            if (index == path.Count - 1)
            {
                if (value == null ? field.FieldType.IsValueType : !field.FieldType.IsInstanceOfType(value)) return false;
                field.SetValue(owner, value);
                return true;
            }

            if (!field.FieldType.IsValueType) return false;
            var nested = field.GetValue(owner);
            if (nested == null || !TryWriteField(nested, path, index + 1, value)) return false;
            field.SetValue(owner, nested);
            return true;
        }

        private static bool IsPoolType(Type type) => type != null && type != typeof(void) && type.IsValueType &&
            !type.ContainsGenericParameters && !type.IsByRefLike && Nullable.GetUnderlyingType(type) == null;

        private static bool Add<T>(SFEcsWorld world, int entity) where T : struct
        {
            var pool = world.GetPool<T>();
            if (pool.Has(entity)) return false;
            pool.Add(entity);
            return true;
        }
    }
}
