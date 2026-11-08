using System;
using SFramework.Core.Runtime;

namespace SFramework.ECS.Runtime
{
    /// <summary>Access to the worlds and shared data of the pipeline injecting this system.</summary>
    public interface ISFEcsContext : ISFDependency
    {
        SFEcsWorld GetWorld(string name = null);
        SFEcsPoolNative<T> GetPool<T>(string worldName = null) where T : struct;
        SFEcsWorld.Mask Filter<T>(string worldName = null) where T : struct;
        T GetShared<T>() where T : class;
    }

    internal sealed class SFEcsContext : ISFEcsContext, ISFResolver
    {
        private readonly ISFEcsSystems _systems;
        private readonly ISFResolver _services;

        internal SFEcsContext(ISFEcsSystems systems, ISFResolver services)
        {
            _systems = systems;
            _services = services;
        }

        public SFEcsWorld GetWorld(string name = null) => RequireWorld(_systems, name);
        public SFEcsPoolNative<T> GetPool<T>(string worldName = null) where T : struct => GetWorld(worldName).GetPool<T>();
        public SFEcsWorld.Mask Filter<T>(string worldName = null) where T : struct => GetWorld(worldName).Filter<T>();
        public T GetShared<T>() where T : class => _systems.GetShared<T>();

        internal static SFEcsWorld RequireWorld(ISFEcsSystems systems, string name)
        {
            var world = systems.GetWorld(name);
            if (world == null || !world.IsAlive())
                throw new InvalidOperationException($"ECS world '{name ?? "[default]"}' is unavailable in this pipeline.");
            return world;
        }

        T ISFResolver.Resolve<T>() => typeof(T) == typeof(ISFEcsContext) ? (T)(object)this : _services.Resolve<T>();
        object ISFResolver.Resolve(Type type) => type == typeof(ISFEcsContext) ? this : _services.Resolve(type);
        bool ISFResolver.IsRegistered(Type type) => type == typeof(ISFEcsContext) || _services.IsRegistered(type);

        bool ISFResolver.TryResolve<T>(out T instance)
        {
            if (typeof(T) != typeof(ISFEcsContext)) return _services.TryResolve(out instance);
            instance = (T)(object)this;
            return true;
        }

        void ISFResolver.ValidateDependency(Type type, string target)
        {
            if (type != typeof(ISFEcsContext)) _services.ValidateDependency(type, target);
        }
    }
}
