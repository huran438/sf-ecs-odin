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
        public EcsPackedEntityWithWorld EcsPackedEntity => _ecsPackedEntity;

        [SFInject]
        private ISFWorldsService _worldsService;

        [SFWorld]
        [SerializeField]
        private string _world;

        [SerializeField]
        private SFEntity _parentEntity;

        private EcsPackedEntityWithWorld _ecsPackedEntity;
        private bool _injected;
        private ISFEntitySetup[] _components;

        protected override void Init()
        {
            if (_injected) return;
            _components = GetComponents<ISFEntitySetup>();
            _injected = true;
        }

        public void OnEnable()
        {
            var world = _worldsService.GetWorld(_world);
            var entity = world.NewEntity();
            _ecsPackedEntity = world.PackEntityWithWorld(entity);

            SFEntityMapping.AddMapping(gameObject, ref _ecsPackedEntity);

            world.GetPool<GameObjectRef>().Add(entity) = new GameObjectRef
            {
                value = gameObject
            };

            var _transform = transform;

            world.GetPool<TransformRef>().Add(entity) = new TransformRef
            {
                value = _transform
            };

            if (_parentEntity != null)
            {
                world.GetPool<ParentEntity>().Add(entity) = new ParentEntity
                {
                    value = _parentEntity.EcsPackedEntity
                };
            }

            foreach (var entitySetup in _components)
            {
                entitySetup.Setup(ref _ecsPackedEntity);
            }

        }

        public void OnDisable()
        {
            SFEntityMapping.RemoveMapping(gameObject);

            if (_ecsPackedEntity.Unpack(out var world, out var entity))
                world.DelEntity(entity);
        }

        private void Reset()
        {
            if (transform.parent != null)
            {
                _parentEntity = transform.parent.GetComponent<SFEntity>();
            }
        }
    }
}