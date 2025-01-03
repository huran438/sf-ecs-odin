using Leopotam.EcsLite;
using SFramework.Core.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    public sealed class SFEntity : SFView, ISFEntity
    {
        public ref EcsPackedEntityWithWorld EcsPackedEntity => ref _ecsPackedEntity;

        [SFInject]
        private readonly ISFWorldsService _worldsService;

        [SFWorld]
        [SerializeField]
        private string _world;

        private EcsPackedEntityWithWorld _ecsPackedEntity;
        private bool _injected;
        private ISFEntitySetup[] _components;
        private EcsPool<GameObjectRef> _gameObjectRefPool;
        private EcsPool<TransformRef> _transformRefPool;
        private EcsPool<RootEntity> _rootEntityPool;
        private EcsWorld _ecsWorld;

        private bool _isRootEntity;

        protected override void Init()
        {
            if (_injected) return;
            _components = GetComponents<ISFEntitySetup>();
            _isRootEntity = transform.parent == null || transform.parent.GetComponentInParent<SFEntity>(true) == null;
            _injected = true;
            _ecsWorld = _worldsService.GetWorld(_world);
            _gameObjectRefPool = _ecsWorld.GetPool<GameObjectRef>();
            _transformRefPool = _ecsWorld.GetPool<TransformRef>();
            _rootEntityPool = _ecsWorld.GetPool<RootEntity>();
        }

        public void OnEnable()
        {
            var entity = _ecsWorld.NewEntity();
            _ecsPackedEntity = _ecsWorld.PackEntityWithWorld(entity);

            SFEntityMapping.AddMapping(gameObject, ref _ecsPackedEntity);

            _gameObjectRefPool.Add(entity) = new GameObjectRef
            {
                value = gameObject
            };
            
            if (_isRootEntity)
            {
                _rootEntityPool.Add(entity) = new RootEntity();
            }

            _transformRefPool.Add(entity) = new TransformRef
            {
                value = transform
            };

            foreach (var entitySetup in _components)
            {
                entitySetup.Setup(ref _ecsPackedEntity);
            }
        }

        public void OnDisable()
        {
            SFEntityMapping.RemoveMapping(gameObject);

            if (_ecsPackedEntity.Unpack(out var world, out var entity))
            {
                world.DelEntity(entity);
            }
        }
    }
}