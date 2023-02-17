using System;
using System.Collections.Generic;
using System.Linq;
using Leopotam.EcsLite;
using Leopotam.EcsLite.Di;
using SFramework.Core.Runtime;
using SFramework.Repositories.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SFramework.ECS.Runtime
{
    public class SFWorldsService : ISFWorldsService, IDisposable
    {
        [SFInject]
        private readonly ISFContainer _container;

        private readonly Dictionary<string, EcsWorld> _ecsWorlds = new();
        private readonly List<EcsSystems> _lateUpdateSystems = new();
        private readonly List<EcsSystems> _updateSystems = new();
        private readonly List<EcsSystems> _fixedUpdateSystems = new();
        private readonly SFECSCallbacks _callbacks;

        private EcsWorld _defaultWorld;
        public EcsWorld[] Worlds => _ecsWorlds.Values.ToArray();

        public EcsWorld GetWorld(string name = "")
        {
            return _ecsWorlds.ContainsKey(name) ? _ecsWorlds[name] : _defaultWorld;
        }


        public SFWorldsService()
        {
            _callbacks = new GameObject("SF_ECS_CALLBACKS").AddComponent<SFECSCallbacks>();
            _callbacks.OnFixedUpdate += FixedUpdate;
            _callbacks.OnUpdate += Update;
            _callbacks.OnLateUpdate += LateUpdate;
        }


        [SFInject]
        public void Init(ISFRepositoryProvider provider)
        {
            var _repository = provider.GetRepositories<SFWorldsRepository>().FirstOrDefault();

            if (_repository == null) return;

            _defaultWorld = new EcsWorld();

            foreach (SFWorldNode worldContainer in _repository.Nodes)
            {
                AddWorldContainer(worldContainer);
            }
        }

        private EcsWorld AddWorldContainer(SFWorldNode worldNode)
        {
            if (_ecsWorlds.ContainsKey(worldNode._Name)) return null;

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

            var fixedUpdateSystems = new EcsSystems(world, _container);
            var updateSystems = new EcsSystems(world, _container);
            var lateUpdateSystems = new EcsSystems(world, _container);

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
            _ecsWorlds[worldNode._Name] = world;
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