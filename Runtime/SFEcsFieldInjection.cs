using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Scripting;

namespace SFramework.ECS.Runtime
{
    public static class EcsFieldInjector
    {
        private static readonly ConcurrentDictionary<Type, FieldInfo[]> BindingFields = new();

        public static void Bind(object target, SFEcsWorld world) => Bind(target, world, null);

        internal static void Bind(object target, ISFEcsContext context) => Bind(target, null, context);

        private static void Bind(object target, SFEcsWorld world, ISFEcsContext context)
        {
            foreach (var field in BindingFields.GetOrAdd(target.GetType(), GetBindingFields))
            {
                if (field.GetValue(target) is not IEcsFieldBinding binding)
                {
                    binding = (IEcsFieldBinding)Activator.CreateInstance(field.FieldType);
                    field.SetValue(target, binding);
                }

                binding.Bind(context == null ? world : context.GetWorld(binding.WorldName));
            }
        }

        private static FieldInfo[] GetBindingFields(Type systemType)
        {
            var bindings = new List<FieldInfo>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var type = systemType; type != null; type = type.BaseType)
            {
                foreach (var field in type.GetFields(flags))
                {
                    if (typeof(IEcsFieldBinding).IsAssignableFrom(field.FieldType))
                        bindings.Add(field);
                }
            }

            return bindings.ToArray();
        }
    }

    internal interface IEcsFieldBinding
    {
        string WorldName { get; }

        void Bind(SFEcsWorld world);
    }

    public sealed class SFEcsPool<T> : IEcsFieldBinding where T : struct
    {
        private readonly string _worldName;

        [Preserve]
        public SFEcsPool() : this(null) { }

        public SFEcsPool(string worldName) => _worldName = worldName;

        public SFEcsPoolNative<T> Value { get; private set; }

        string IEcsFieldBinding.WorldName => _worldName;

        public SFEcsPoolNative<T> ForWorld(SFEcsWorld world) => world.GetPool<T>();

        public void BindWorld(SFEcsWorld world) => Value = ForWorld(world);

        void IEcsFieldBinding.Bind(SFEcsWorld world) => BindWorld(world);
    }

    public sealed class EcsWorldInject : IEcsFieldBinding
    {
        [Preserve]
        public EcsWorldInject() { }

        public SFEcsWorld Value { get; private set; }

        string IEcsFieldBinding.WorldName => null;

        void IEcsFieldBinding.Bind(SFEcsWorld world) => Value = world;
    }

    public class SFEcsFilter<TInclude, TExclude> : IEcsFieldBinding
        where TInclude : struct, IEcsInclude
        where TExclude : struct, IEcsExclude
    {
        private readonly string _worldName;

        [Preserve]
        public SFEcsFilter() : this(null) { }

        public SFEcsFilter(string worldName) => _worldName = worldName;

        public SFEcsFilterNative Value { get; private set; }

        string IEcsFieldBinding.WorldName => _worldName;

        void IEcsFieldBinding.Bind(SFEcsWorld world)
        {
            var mask = default(TInclude).CreateMask(world);
            Value = default(TExclude).ExcludeFrom(mask).End();
        }
    }

    public sealed class SFEcsFilter<TInclude> : SFEcsFilter<TInclude, NoExcludes>
        where TInclude : struct, IEcsInclude
    {
        [Preserve]
        public SFEcsFilter() { }

        public SFEcsFilter(string worldName) : base(worldName) { }
    }
}
