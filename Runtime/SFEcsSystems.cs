// ----------------------------------------------------------------------------
// The MIT-Red License
// Copyright (c) 2012-2026 Leopotam <leopotam@yandex.ru>
// See SFEcsWorld.cs for the MIT-Red license terms.
// ----------------------------------------------------------------------------

using System.Collections.Generic;
using SFramework.Core.Runtime;

#if ENABLE_IL2CPP
using Unity.IL2CPP.CompilerServices;
#endif

namespace SFramework.ECS.Runtime
{
    public interface ISFEcsSystem
    {
    }

    public interface ISFEcsPreInitSystem : ISFEcsSystem
    {
        void PreInit(ISFEcsSystems systems);
    }

    public interface ISFEcsInitSystem : ISFEcsSystem
    {
        void Init(ISFEcsSystems systems);
    }

    public interface ISFEcsRunSystem : ISFEcsSystem
    {
        void Run(ISFEcsSystems systems);
    }

    public interface ISFEcsPostRunSystem : ISFEcsSystem
    {
        void PostRun(ISFEcsSystems systems);
    }

    public interface ISFEcsDestroySystem : ISFEcsSystem
    {
        void Destroy(ISFEcsSystems systems);
    }

    public interface ISFEcsPostDestroySystem : ISFEcsSystem
    {
        void PostDestroy(ISFEcsSystems systems);
    }

    public interface ISFEcsSystems
    {
        T GetShared<T>() where T : class;
        ISFEcsSystems AddWorld(SFEcsWorld world, string name);
        SFEcsWorld GetWorld(string name = null);
        Dictionary<string, SFEcsWorld> GetAllNamedWorlds();
        ISFEcsSystems Add(ISFEcsSystem system);
        List<ISFEcsSystem> GetAllSystems();
        void Init();
        void Run();
        void Destroy();
    }

#if ENABLE_IL2CPP
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
#endif
    public class SFEcsSystems : ISFEcsSystems
    {
        private readonly SFEcsWorld _defaultWorld;
        private readonly Dictionary<string, SFEcsWorld> _worlds;
        private readonly List<ISFEcsSystem> _allSystems;
        private readonly List<ISFEcsRunSystem> _runSystems;
        private readonly List<ISFEcsPostRunSystem> _postRunSystems;
        private readonly object _shared;
#if UNITY_EDITOR
        internal SFEcsSystemsProfiler.SystemSlots ProfilerSlots { get; } = new();
        internal (string WorldName, string Phase) ProfilerLocation { get; set; }
        internal bool IsProfilerWorldAlive => _defaultWorld?.IsAlive() != false;
#endif
#if DEBUG
        protected bool _inited;
#endif

        public SFEcsSystems(SFEcsWorld defaultWorld, object shared = null)
        {
            _defaultWorld = defaultWorld;
            _shared = shared;
            _worlds = new Dictionary<string, SFEcsWorld>(8);
            _allSystems = new List<ISFEcsSystem>(128);
            _runSystems = new List<ISFEcsRunSystem>(128);
            _postRunSystems = new List<ISFEcsPostRunSystem>(128);
        }

        public virtual T GetShared<T>() where T : class
        {
            return _shared as T;
        }

        public virtual ISFEcsSystems AddWorld(SFEcsWorld world, string name)
        {
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
            if (_inited)
            {
                throw new System.Exception("Cant add world after initialization.");
            }
            if (world == null)
            {
                throw new System.Exception("World cant be null.");
            }
            if (string.IsNullOrEmpty(name))
            {
                throw new System.Exception("World name cant be null or empty.");
            }
            if (_worlds.ContainsKey(name))
            {
                throw new System.Exception($"World with name \"{name}\" already added.");
            }
#endif
            _worlds[name] = world;
#if UNITY_EDITOR
            if (world != null && !string.IsNullOrEmpty(name)) world.DebugName = name;
#endif
            return this;
        }

        public virtual SFEcsWorld GetWorld(string name = null)
        {
            if (name == null)
            {
                return _defaultWorld;
            }
            _worlds.TryGetValue(name, out var world);
            return world;
        }

        public virtual Dictionary<string, SFEcsWorld> GetAllNamedWorlds()
        {
            return _worlds;
        }

        public virtual ISFEcsSystems Add(ISFEcsSystem system)
        {
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
            if (_inited)
            {
                throw new System.Exception("Cant add system after initialization.");
            }
#endif
            _allSystems.Add(system);
            if (system is ISFEcsRunSystem runSystem)
            {
                _runSystems.Add(runSystem);
            }
            if (system is ISFEcsPostRunSystem postRunSystem)
            {
                _postRunSystems.Add(postRunSystem);
            }
#if UNITY_EDITOR
            ProfilerSlots.Add(system);
#endif
            return this;
        }

        public virtual List<ISFEcsSystem> GetAllSystems()
        {
            return _allSystems;
        }

        public virtual void Init()
        {
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
            if (_inited)
            {
                throw new System.Exception("Already initialized.");
            }
#endif
            // Use the SF Core generated injector with this pipeline's ECS context.
            var resolver = new SFEcsContext(this, SFContainer.Current);
            foreach (var system in _allSystems)
            {
                InjectSystem(system, resolver);
            }
            foreach (var system in _allSystems)
            {
                if (system is ISFEcsPreInitSystem initSystem)
                {
                    initSystem.PreInit(this);
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
                    var worldName = CheckForLeakedEntities(this);
                    if (worldName != null)
                    {
                        throw new System.Exception($"Empty entity detected in world \"{worldName}\" after {initSystem.GetType().Name}.PreInit().");
                    }
#endif
                }
            }
            foreach (var system in _allSystems)
            {
                if (system is ISFEcsInitSystem initSystem)
                {
                    initSystem.Init(this);
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
                    var worldName = CheckForLeakedEntities(this);
                    if (worldName != null)
                    {
                        throw new System.Exception($"Empty entity detected in world \"{worldName}\" after {initSystem.GetType().Name}.Init().");
                    }
#endif
                }
            }
#if DEBUG
            _inited = true;
#endif
#if UNITY_EDITOR
            var (profileWorldName, profilePhase) = ProfilerLocation;
            SFEcsSystemsProfiler.Track(this, profileWorldName ?? _defaultWorld?.DebugName ?? "Unnamed world",
                profilePhase ?? "Manual");
#endif
        }

        private static void InjectSystem(ISFEcsSystem system, SFEcsContext resolver)
        {
            if (system is ISFGeneratedInjectable generated)
            {
                generated.ValidateDependencies(resolver);
                generated.InjectDependencies(resolver);
            }
            EcsFieldInjector.Bind(system, resolver);

            if (system is ISFInjectable && system is ISFInjectionCallback callback)
                callback.OnDependenciesInjected();

            if (system is SFEcsGroupSystem group)
            {
                foreach (var child in group.NestedSystems) InjectSystem(child, resolver);
            }
        }

        public virtual void Run()
        {
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
            if (!_inited)
            {
                throw new System.Exception("Cant run without initialization.");
            }
#endif
            for (int i = 0, iMax = _runSystems.Count; i < iMax; i++)
            {
#if UNITY_EDITOR
                using (SFEcsSystemsProfiler.Measure(this, i, postRun: false))
#endif
                    _runSystems[i].Run(this);
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
                var worldName = CheckForLeakedEntities(this);
                if (worldName != null)
                {
                    throw new System.Exception($"Empty entity detected in world \"{worldName}\" after {_runSystems[i].GetType().Name}.Run().");
                }
#endif
            }
            for (int i = 0, iMax = _postRunSystems.Count; i < iMax; i++)
            {
#if UNITY_EDITOR
                using (SFEcsSystemsProfiler.Measure(this, i, postRun: true))
#endif
                    _postRunSystems[i].PostRun(this);
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
                var worldName = CheckForLeakedEntities(this);
                if (worldName != null)
                {
                    throw new System.Exception($"Empty entity detected in world \"{worldName}\" after {_postRunSystems[i].GetType().Name}.PostRun().");
                }
#endif
            }
        }

        public virtual void Destroy()
        {
            Destroy(strict: true);
        }

        internal void DestroyOwnedPipeline()
        {
            Destroy(strict: false);
        }

        private void Destroy(bool strict)
        {
            List<System.Exception> failures = null;
            for (var i = _allSystems.Count - 1; i >= 0; i--)
            {
                if (_allSystems[i] is ISFEcsDestroySystem destroySystem)
                {
                    try
                    {
                        destroySystem.Destroy(this);
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
                        var worldName = strict ? CheckForLeakedEntities(this) : null;
                        if (worldName != null)
                            throw new System.Exception($"Empty entity detected in world \"{worldName}\" after {destroySystem.GetType().Name}.Destroy().");
#endif
                    }
                    catch (System.Exception exception) when (!strict) { (failures ??= new List<System.Exception>()).Add(exception); }
                }
            }
            for (var i = _allSystems.Count - 1; i >= 0; i--)
            {
                if (_allSystems[i] is ISFEcsPostDestroySystem postDestroySystem)
                {
                    try
                    {
                        postDestroySystem.PostDestroy(this);
#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
                        var worldName = strict ? CheckForLeakedEntities(this) : null;
                        if (worldName != null)
                            throw new System.Exception($"Empty entity detected in world \"{worldName}\" after {postDestroySystem.GetType().Name}.PostDestroy().");
#endif
                    }
                    catch (System.Exception exception) when (!strict) { (failures ??= new List<System.Exception>()).Add(exception); }
                }
            }
            _worlds.Clear();
            _allSystems.Clear();
            _runSystems.Clear();
            _postRunSystems.Clear();
#if UNITY_EDITOR
            ProfilerSlots.Clear();
            SFEcsSystemsProfiler.Untrack(this);
#endif
#if DEBUG
            _inited = false;
#endif
            if (failures != null) throw new System.AggregateException("ECS pipeline shutdown failed.", failures);
        }

#if DEBUG && !SFECS_NO_SANITIZE_CHECKS
        public static string CheckForLeakedEntities(ISFEcsSystems systems)
        {
            var defaultWorld = systems.GetWorld();
            if (defaultWorld.CheckForLeakedEntities())
            {
                return "default";
            }
            foreach (var pair in systems.GetAllNamedWorlds())
            {
                if (pair.Value != defaultWorld && pair.Value.CheckForLeakedEntities())
                {
                    return pair.Key;
                }
            }
            return null;
        }
#endif
    }
}
