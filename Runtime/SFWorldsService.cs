using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using SFramework.Configs.Runtime;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    public partial class SFWorldsService : ISFWorldsService
    {
        private readonly Dictionary<string, SFEcsWorld> _ecsWorlds = new();
        private readonly Dictionary<string, SFEcsWorld> _shortIds = new();
        private readonly List<SFEcsSystems> _lateUpdateSystems = new();
        private readonly List<SFEcsSystems> _updateSystems = new();
        private readonly List<SFEcsSystems> _fixedUpdateSystems = new();
        private readonly List<SFEcsSystems> _startedPipelines = new();
        private readonly ISFConfigsService _configsService;
        private SFECSCallbacks _callbacks;
        private bool _initializing;
        private bool _initialized;
        private bool _disposed;
        internal bool IsWaitingForInitialization => !_initialized && !_disposed;
        internal event Action Initialized;

        private readonly SFEcsWorld _defaultWorld;
        public SFEcsWorld[] Worlds => _ecsWorlds.Values.ToArray();

        public SFEcsWorld GetWorld(string name = "")
        {
            if (string.IsNullOrEmpty(name)) return _defaultWorld;
            if (_ecsWorlds.TryGetValue(name, out var world)) return world;
            return _shortIds.GetValueOrDefault(name) ?? _defaultWorld;
        }

        SFWorldsService(ISFConfigsService provider)
        {
            _configsService = provider;
            _defaultWorld = new SFEcsWorld();
#if UNITY_EDITOR
            _defaultWorld.DebugName = "Fallback";
#endif
        }

        public UniTask Init(CancellationToken cancellationToken)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SFWorldsService));
            if (_initializing) throw new InvalidOperationException("ECS worlds are already initializing.");
            if (_initialized) return UniTask.CompletedTask;

            _initializing = true;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var registered = new List<(string Id, SFWorldNode Node)>();
                if (_configsService.TryGetConfigs(out SFWorldsConfig[] worldConfigs) && worldConfigs != null)
                {
                    foreach (var worldsConfig in worldConfigs)
                    {
                        if (worldsConfig?.Worlds == null) continue;
                        foreach (var worldNode in worldsConfig.Worlds)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (TryRegisterWorld(worldsConfig.Id, worldNode, out var id))
                                registered.Add((id, worldNode));
                        }
                    }
                }

                foreach (var (id, node) in registered)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var world = _ecsWorlds[id];
                    _fixedUpdateSystems.Add(StartPipeline(world, id, "FixedUpdate", node.FixedUpdateSystems));
                    _updateSystems.Add(StartPipeline(world, id, "Update", node.UpdateSystems));
                    _lateUpdateSystems.Add(StartPipeline(world, id, "LateUpdate", node.LateUpdateSystems));
                }

                cancellationToken.ThrowIfCancellationRequested();
                _callbacks = new GameObject("SF_ECS_CALLBACKS").AddComponent<SFECSCallbacks>();
                _callbacks.OnFixedUpdate += FixedUpdate;
                _callbacks.OnUpdate += Update;
                _callbacks.OnLateUpdate += LateUpdate;
                _initialized = true;
            }
            catch (Exception initializationFailure)
            {
                var failures = new List<Exception>();
                ReleaseConfiguredWorlds(failures);
                if (failures.Count == 0) throw;
                failures.Insert(0, initializationFailure);
                throw new AggregateException("ECS initialization and rollback failed.", failures);
            }
            finally
            {
                _initializing = false;
            }

            var listeners = Initialized;
            Initialized = null;
            if (listeners != null)
            {
                foreach (Action listener in listeners.GetInvocationList())
                {
                    try { listener(); }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
            }
            return UniTask.CompletedTask;
        }

        private bool TryRegisterWorld(string configId, SFWorldNode worldNode, out string fullId)
        {
            fullId = null;
            if (string.IsNullOrWhiteSpace(worldNode?.Id))
            {
                Debug.LogWarning($"Skipping an ECS world with no ID in configuration '{configId}'.");
                return false;
            }

            fullId = string.IsNullOrEmpty(configId) ? worldNode.Id : $"{configId}/{worldNode.Id}";
            if (_ecsWorlds.ContainsKey(fullId)) return false;

            var config = worldNode.Config ?? new SFWorldConfig();
            var world = new SFEcsWorld(new SFEcsWorld.Config
            {
                Entities = config.Entities,
                RecycledEntities = config.RecycledEntities,
                Pools = config.Pools,
                Filters = config.Filters,
                PoolDenseSize = config.PoolDenseSize,
                PoolRecycledSize = config.PoolRecycledSize,
                EntityComponentsSize = config.EntityComponentsSize
            });

#if UNITY_EDITOR
            world.DebugName = fullId;
#endif
            _ecsWorlds.Add(fullId, world);
            if (!_shortIds.TryAdd(worldNode.Id, world)) _shortIds[worldNode.Id] = null;
            return true;
        }

        private SFEcsSystems StartPipeline(SFEcsWorld world, string worldId, string phase, SFSystemContainer[] entries)
        {
            var pipeline = new SFEcsSystems(world);
#if UNITY_EDITOR
            pipeline.ProfilerLocation = (worldId, phase);
#endif
            foreach (var (id, namedWorld) in _ecsWorlds) pipeline.AddWorld(namedWorld, id);
            foreach (var entry in entries ?? Array.Empty<SFSystemContainer>())
            {
                if (entry?.Enabled != true) continue;
                var type = SFSerializableType.ResolveTypeName(entry.System);
                if (type == null || type.IsAbstract || type.ContainsGenericParameters ||
                    !typeof(ISFEcsSystem).IsAssignableFrom(type) ||
                    (!type.IsValueType && type.GetConstructor(Type.EmptyTypes) == null))
                {
                    Debug.LogWarning($"Skipping ECS system '{entry.System}' in world '{worldId}', phase '{phase}'. " +
                        "Expected a concrete ISFEcsSystem with a public parameterless constructor.");
                    continue;
                }

                pipeline.Add((ISFEcsSystem)Activator.CreateInstance(type));
            }

            _startedPipelines.Add(pipeline);
            pipeline.Init();
            return pipeline;
        }

        private void FixedUpdate()
        {
            foreach (var systems in _fixedUpdateSystems)
            {
                systems.Run();
            }
        }

        private void Update()
        {
            foreach (var systems in _updateSystems)
            {
                systems.Run();
            }
        }

        private void LateUpdate()
        {
            foreach (var systems in _lateUpdateSystems)
            {
                systems.Run();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_initializing) throw new InvalidOperationException("Cannot dispose ECS worlds while they are initializing.");
            _disposed = true;
            Initialized = null;

            var failures = new List<Exception>();
            ReleaseConfiguredWorlds(failures);
            TryRelease(() => ReleaseWorld(_defaultWorld), failures);
            if (failures.Count > 0) throw new AggregateException("ECS shutdown failed.", failures);
        }

        private void ReleaseConfiguredWorlds(List<Exception> failures)
        {
            if (_callbacks != null)
            {
                _callbacks.OnFixedUpdate -= FixedUpdate;
                _callbacks.OnUpdate -= Update;
                _callbacks.OnLateUpdate -= LateUpdate;
                var gameObject = _callbacks.gameObject;
                _callbacks = null;
                if (Application.isPlaying) UnityEngine.Object.Destroy(gameObject);
                else UnityEngine.Object.DestroyImmediate(gameObject);
            }

            _fixedUpdateSystems.Clear();
            _updateSystems.Clear();
            _lateUpdateSystems.Clear();
            for (var i = _startedPipelines.Count - 1; i >= 0; i--)
            {
                TryRelease(_startedPipelines[i].DestroyOwnedPipeline, failures);
            }
            _startedPipelines.Clear();

            foreach (var world in _ecsWorlds.Values)
            {
                TryRelease(() => ReleaseWorld(world), failures);
            }
            _ecsWorlds.Clear();
            _shortIds.Clear();
        }

        private static void ReleaseWorld(SFEcsWorld world)
        {
            SFEntityMapping.RemoveMappings(world);
            world.DestroyOwnedWorld();
        }

        private static void TryRelease(Action release, List<Exception> failures)
        {
            try { release(); }
            catch (Exception exception) { failures.Add(exception); }
        }
    }
}
