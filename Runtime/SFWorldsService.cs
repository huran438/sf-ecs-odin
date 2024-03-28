using System;
using System.Collections.Generic;
using System.Linq;
using Leopotam.EcsLite;
using Leopotam.EcsLite.Di;
using SFramework.Configs.Runtime;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    public class SFWorldsService : ISFWorldsService
    {
        private readonly Dictionary<string, EcsWorld> _ecsWorlds = new();
        private readonly List<EcsSystems> _lateUpdateSystems = new();
        private readonly List<EcsSystems> _updateSystems = new();
        private readonly List<EcsSystems> _fixedUpdateSystems = new();
        private readonly SFECSCallbacks _callbacks;

        private EcsWorld _defaultWorld;
        public EcsWorld[] Worlds => _ecsWorlds.Values.ToArray();

        public EcsWorld GetWorld(string name = "")
        {
            return _ecsWorlds.TryGetValue(name, out var world) ? world : _defaultWorld;
        }

        SFWorldsService(ISFConfigsService provider)
        {
            _defaultWorld = new EcsWorld();
            _callbacks = new GameObject("SF_ECS_CALLBACKS").AddComponent<SFECSCallbacks>();
            _callbacks.OnFixedUpdate += FixedUpdate;
            _callbacks.OnUpdate += Update;
            _callbacks.OnLateUpdate += LateUpdate;
            var _repository = provider.GetRepositories<SFWorldsConfig>().FirstOrDefault();
            if (_repository == null) return;
            foreach (SFWorldNode worldContainer in _repository.Children)
            {
                AddWorldContainer(worldContainer);
            }
        }

        private EcsWorld AddWorldContainer(SFWorldNode worldNode)
        {
            if (_ecsWorlds.ContainsKey(worldNode.Id)) return null;

            var world = new EcsWorld(new EcsWorld.Config
            {
                Entities = worldNode.Config.Entities,
                RecycledEntities = worldNode.Config.RecycledEntities,
                Pools = worldNode.Config.Pools,
                Filters = worldNode.Config.Filters,
                PoolDenseSize = worldNode.Config.PoolDenseSize,
                PoolRecycledSize = worldNode.Config.PoolRecycledSize,
                EntityComponentsSize = worldNode.Config.EntityComponentsSize
            });

            var fixedUpdateSystems = new EcsSystems(world);
            var updateSystems = new EcsSystems(world);
            var lateUpdateSystems = new EcsSystems(world);

            foreach (var system in worldNode.FixedUpdateSystems)
            {
                if (!system.Enabled) continue;
                var systemType = Type.GetType(system.System);
                if (systemType == null) continue;
                var systemInstance = Activator.CreateInstance(systemType) as IEcsSystem;
                if (systemInstance == null) continue;
                fixedUpdateSystems.Add(systemInstance);
            }

            foreach (var system in worldNode.UpdateSystems)
            {
                if (!system.Enabled) continue;
                var systemType = Type.GetType(system.System);
                if (systemType == null) continue;
                var systemInstance = Activator.CreateInstance(systemType) as IEcsSystem;
                if (systemInstance == null) continue;
                updateSystems.Add(systemInstance);
            }

            foreach (var system in worldNode.LateUpdateSystems)
            {
                if (!system.Enabled) continue;
                var systemType = Type.GetType(system.System);
                if (systemType == null) continue;
                var systemInstance = Activator.CreateInstance(systemType) as IEcsSystem;
                if (systemInstance == null) continue;
                lateUpdateSystems.Add(systemInstance);
            }

            fixedUpdateSystems.Inject().Init();
            updateSystems.Inject().Init();
            lateUpdateSystems.Inject().Init();


            _fixedUpdateSystems.Add(fixedUpdateSystems);
            _updateSystems.Add(updateSystems);
            _lateUpdateSystems.Add(lateUpdateSystems);
            _ecsWorlds[worldNode.Id] = world;
            return world;
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
            _fixedUpdateSystems.Clear();
            _updateSystems.Clear();
            _lateUpdateSystems.Clear();

            _callbacks.OnFixedUpdate -= FixedUpdate;
            _callbacks.OnUpdate -= Update;
            _callbacks.OnLateUpdate -= LateUpdate;
        }
    }
}