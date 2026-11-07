using System;
using System.Collections.Generic;
using SFramework.Core.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    [HideMonoScript]
    [DisallowMultipleComponent]
    public sealed partial class SFEntity : SFView, ISFEntity
    {
        public ref SFEcsPackedEntityWithWorld EcsPackedEntity => ref _ecsPackedEntity;

        private ISFWorldsService _worldsService;

        [SFWorld] [SerializeField] private string _world;

        public bool removeEntityOnDisable;

        private SFEcsPackedEntityWithWorld _ecsPackedEntity;
        private bool _injected;
        private ISFEntitySetup[] _components;
        private SFEcsPool<GameObjectRef> _gameObjectRefPool;
        private SFEcsPool<TransformRef> _transformRefPool;
        private SFEcsPool<RootEntity> _rootEntityPool;
        private SFEcsWorld _ecsWorld;
        private Action _cancelPendingCreation;

        [SerializeField] [HideInInspector] private bool _isRootEntity;
        [SerializeField] [HideInInspector] private bool _checkedRootEntity;

        private IReadOnlyList<SFEntity> _childrenEntities;

        private bool _createdEntity;

        protected override void Init()
        {
            if (_injected) return;
            _components = GetComponents<ISFEntitySetup>();
            _injected = true;

            if (_checkedRootEntity == false)
            {
                OnValidate();
            }
        }

        private void OnEnable()
        {
            if (_worldsService is SFWorldsService service && service.IsWaitingForInitialization && !string.IsNullOrEmpty(_world))
            {
                CancelPendingCreation();
                service.Initialized += CreatePendingEntity;
                _cancelPendingCreation = () => service.Initialized -= CreatePendingEntity;
                return;
            }

            if (_worldsService != null) CreateEntity();
        }

        private void Start()
        {
            if (_worldsService != null) return;
            this.Inject();
            if (_worldsService != null) OnEnable();
            else Debug.LogError($"{nameof(SFEntity)} requires a registered {nameof(ISFWorldsService)}.", this);
        }

        private void CreatePendingEntity()
        {
            CancelPendingCreation();
            if (enabled && gameObject.activeInHierarchy) CreateEntity();
        }

        private void CancelPendingCreation()
        {
            _cancelPendingCreation?.Invoke();
            _cancelPendingCreation = null;
        }

        private void OnDisable()
        {
            CancelPendingCreation();
            if (removeEntityOnDisable == false) return;
            DestroyEntity();
        }

        private void OnDestroy()
        {
            CancelPendingCreation();
            if (_createdEntity == false) return;
            DestroyEntity();
        }

        private void OnValidate()
        {
            _isRootEntity = transform.parent == null || transform.parent.GetComponentInParent<SFEntity>(true) == null;
            _checkedRootEntity = true;
        }

        [Button("Recreate Entity")]
        public void RecreateEntity()
        {
            DestroyEntity();
            CreateEntity();
        }

        public void CreateEntity()
        {
            if (_createdEntity) return;
            if (_worldsService == null) throw new InvalidOperationException($"{nameof(SFEntity)} has not received its worlds service.");
            var world = _worldsService.GetWorld(_world);
            if (!world.IsAlive()) throw new InvalidOperationException("Cannot create an entity in a disposed ECS world.");
            if (world != _ecsWorld)
            {
                _ecsWorld = world;
                _gameObjectRefPool = world.GetPool<GameObjectRef>();
                _transformRefPool = world.GetPool<TransformRef>();
                _rootEntityPool = world.GetPool<RootEntity>();
            }

            var entity = _ecsWorld.NewEntity();
            var packed = _ecsWorld.PackEntityWithWorld(entity);
            _ecsPackedEntity = packed;
            _createdEntity = true;
            try
            {
                SFEntityMapping.AddMapping(gameObject, ref _ecsPackedEntity);
                _gameObjectRefPool.Add(entity) = new GameObjectRef { value = gameObject };

                if (_isRootEntity) _rootEntityPool.Add(entity) = new RootEntity();

                _transformRefPool.Add(entity) = new TransformRef { value = transform };
                foreach (var entitySetup in _components)
                {
                    entitySetup.Setup(ref _ecsPackedEntity);
                }
            }
            catch
            {
                _ecsPackedEntity = packed;
                DestroyEntity();
                throw;
            }
        }

        public void DestroyEntity()
        {
            SFEntityMapping.RemoveMapping(gameObject);
            var packed = _ecsPackedEntity;
            _ecsPackedEntity = default;
            _createdEntity = false;

            if (packed.Unpack(out var world, out var entity))
            {
                world.DelEntity(entity);
            }
        }
    }
}
