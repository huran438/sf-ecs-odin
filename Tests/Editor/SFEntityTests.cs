using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using SFramework.ECS.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SFramework.ECS.Tests
{
    public sealed class SFEntityTests
    {
        private SFEcsWorld _world;
        private readonly List<GameObject> _objects = new();
        private Dictionary<int, SFEcsPackedEntityWithWorld> _savedMappings;

        [SetUp]
        public void SetUp()
        {
            _world = new SFEcsWorld();
            _savedMappings = new Dictionary<int, SFEcsPackedEntityWithWorld>(Mappings);
            SFEntityMapping.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _objects)
            {
                if (gameObject == null) continue;
                var entity = gameObject.GetComponent<SFEntity>();
                if (entity != null) entity.DestroyEntity();
                Object.DestroyImmediate(gameObject);
            }
            _objects.Clear();
            if (_world.IsAlive()) _world.Destroy();
            SFEntityMapping.Clear();
            foreach (var pair in _savedMappings) Mappings.Add(pair.Key, pair.Value);
        }

        [Test]
        public void CreateEntityPublishesReferencesMappingAndAutoInitializedComponent()
        {
            var view = NewEntity(withComponent: true);

            view.CreateEntity();

            Assert.That(SFEntityMapping.GetEntity(view.gameObject, out var world, out var entity), Is.True);
            Assert.That(world, Is.SameAs(_world));
            Assert.That(world.GetPool<GameObjectRef>().Get(entity).value, Is.SameAs(view.gameObject));
            Assert.That(world.GetPool<TransformRef>().Get(entity).value, Is.SameAs(view.transform));
            Assert.That(world.GetPool<RootEntity>().Has(entity), Is.True);
            Assert.That(world.GetPool<EcsTestPayload>().Get(entity).Value, Is.EqualTo(20));
            Assert.That(world.GetPool<EcsTestPayload>().Get(entity).Initializations, Is.EqualTo(1));
            Assert.That(view.gameObject.TryGetEntity(out var mapped), Is.True);
            Assert.That(mapped, Is.EqualTo(entity));
        }

        [Test]
        public void CreatingAnAlreadyCreatedEntityPreservesTheLiveEntityAndComponentState()
        {
            var view = NewEntity(withComponent: true);
            view.CreateEntity();
            var original = view.EcsPackedEntity;
            Assert.That(original.Unpack(out _, out var entity), Is.True);
            _world.GetPool<EcsTestPayload>().Get(entity).Value = 99;

            view.CreateEntity();

            Assert.That(view.EcsPackedEntity.EqualsTo(original), Is.True);
            Assert.That(_world.GetPool<EcsTestPayload>().Get(entity).Value, Is.EqualTo(99));
            Assert.That(_world.GetPool<EcsTestPayload>().Get(entity).Initializations, Is.EqualTo(1));
            Assert.That(_world.Filter<GameObjectRef>().End().GetEntitiesCount(), Is.EqualTo(1));
        }

        [Test]
        public void FailedProviderSetupRemovesTheMappingAndAllowsRetryWithoutLeakingAnEntity()
        {
            var view = NewEntity();
            var provider = new FailingProvider();
            typeof(SFEntity).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(view, new ISFEntitySetup[] { provider });

            Assert.Throws<InvalidOperationException>(view.CreateEntity);
            Assert.That(SFEntityMapping.GetEntityPacked(view.gameObject, out _), Is.False);
            Assert.That(provider.FirstEntity.Unpack(out _, out _), Is.False);
            Assert.That(_world.Filter<GameObjectRef>().End().GetEntitiesCount(), Is.Zero);

            view.CreateEntity();
            Assert.That(view.EcsPackedEntity.Unpack(out _, out _), Is.True);
            Assert.That(_world.Filter<GameObjectRef>().End().GetEntitiesCount(), Is.EqualTo(1));
        }

        [Test]
        public void ChildEntityDoesNotReceiveTheRootTag()
        {
            var parent = NewEntity();
            var child = NewEntity(parent: parent.transform);

            parent.CreateEntity();
            child.CreateEntity();

            Assert.That(parent.EcsPackedEntity.Unpack(out _, out var parentId), Is.True);
            Assert.That(child.EcsPackedEntity.Unpack(out _, out var childId), Is.True);
            Assert.That(_world.GetPool<RootEntity>().Has(parentId), Is.True);
            Assert.That(_world.GetPool<RootEntity>().Has(childId), Is.False);
            Assert.That(_world.GetPool<GameObjectRef>().Get(childId).value, Is.SameAs(child.gameObject));
        }

        [Test]
        public void DestroyEntityInvalidatesItsHandleAndRemovesTheGameObjectMapping()
        {
            var view = NewEntity(withComponent: true);
            view.CreateEntity();
            var packed = view.EcsPackedEntity;

            view.DestroyEntity();
            view.DestroyEntity();

            Assert.That(packed.Unpack(out _, out _), Is.False);
            Assert.That(SFEntityMapping.GetEntity(view.gameObject, out _), Is.False);
            Assert.That(view.gameObject.TryGetEntityPacked(out _), Is.False);
            Assert.That(_world.Filter<EcsTestPayload>().End().GetEntitiesCount(), Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisablePolicyControlsWhetherReenableRetainsOrRebuildsTheEntity(bool removeOnDisable)
        {
            var view = NewEntity(withComponent: true);
            view.removeEntityOnDisable = removeOnDisable;
            view.CreateEntity();
            var original = view.EcsPackedEntity;
            Assert.That(original.Unpack(out _, out var originalId), Is.True);
            _world.GetPool<EcsTestPayload>().Get(originalId).Value = 99;

            // EditMode does not drive ordinary MonoBehaviour callbacks. Exercise the
            // actual callbacks explicitly without entering play mode or loading a scene.
            InvokeLifecycle(view, "OnDisable");
            Assert.That(original.Unpack(out _, out _), Is.EqualTo(!removeOnDisable));
            Assert.That(SFEntityMapping.GetEntity(view.gameObject, out _), Is.EqualTo(!removeOnDisable));
            InvokeLifecycle(view, "OnEnable");

            Assert.That(view.EcsPackedEntity.Unpack(out _, out var entity), Is.True);
            Assert.That(view.EcsPackedEntity.EqualsTo(original), Is.EqualTo(!removeOnDisable));
            Assert.That(_world.GetPool<EcsTestPayload>().Get(entity).Value, Is.EqualTo(removeOnDisable ? 27 : 99));
            Assert.That(_world.GetPool<EcsTestPayload>().Get(entity).Initializations, Is.EqualTo(removeOnDisable ? 2 : 1));
            Assert.That(_world.Filter<GameObjectRef>().End().GetEntitiesCount(), Is.EqualTo(1));
        }

        [Test]
        public void RecreateEntityReplacesTheLiveHandleAndReinitializesSerializedComponents()
        {
            var view = NewEntity(withComponent: true);
            view.CreateEntity();
            var original = view.EcsPackedEntity;
            Assert.That(original.Unpack(out _, out var originalId), Is.True);
            _world.GetPool<EcsTestPayload>().Get(originalId).Value = 99;

            view.RecreateEntity();

            Assert.That(original.Unpack(out _, out _), Is.False, "The replaced handle must be invalidated.");
            Assert.That(view.EcsPackedEntity.Unpack(out _, out var entity), Is.True, "RecreateEntity must create a replacement entity.");
            Assert.That(SFEntityMapping.GetEntity(view.gameObject, out var world, out var mapped), Is.True);
            Assert.That(world, Is.SameAs(_world));
            Assert.That(mapped, Is.EqualTo(entity));
            Assert.That(_world.GetPool<EcsTestPayload>().Get(entity).Value, Is.EqualTo(27));
            Assert.That(_world.GetPool<EcsTestPayload>().Get(entity).Initializations, Is.EqualTo(2));
        }

        [Test]
        public void MappingDoesNotResolveADeletedEntityAfterItsIdIsRecycled()
        {
            var view = NewEntity();
            view.CreateEntity();
            var original = view.EcsPackedEntity;
            Assert.That(original.Unpack(out _, out var entity), Is.True);
            _world.DelEntity(entity);
            var recycled = _world.NewEntity();
            _world.GetPool<EcsTestPayload>().Add(recycled) = new EcsTestPayload { Value = 42 };

            Assert.That(view.gameObject.TryGetEntity(out _), Is.False);
            Assert.That(view.gameObject.TryGetEntityPacked(out var stale), Is.True, "Packed lookup exposes mapping presence, not liveness.");
            Assert.That(stale.Unpack(out _, out _), Is.False);
            Assert.That(_world.GetPool<EcsTestPayload>().Get(recycled).Value, Is.EqualTo(42));
        }

        [Test]
        public void ComponentSetupIgnoresAnInvalidPackedEntity()
        {
            var gameObject = NewObject();
            var component = gameObject.AddComponent<EcsTestComponent>();
            component.SetSerializedValue(13);
            var invalid = default(SFEcsPackedEntityWithWorld);

            Assert.DoesNotThrow(() => component.Setup(ref invalid));

            Assert.That(_world.Filter<EcsTestPayload>().End().GetEntitiesCount(), Is.Zero);
        }

        [Test]
        public void ReplaceAddsAMissingComponentAndUpdatesAnExistingComponentWithoutDuplication()
        {
            var entity = _world.NewEntity();
            var pool = _world.GetPool<EcsTestPayload>();

            pool.Replace(entity) = new EcsTestPayload { Value = 5 };
            pool.Replace(entity).Value += 9;

            Assert.That(pool.Get(entity).Value, Is.EqualTo(14));
            Assert.That(_world.Filter<EcsTestPayload>().End().GetEntitiesCount(), Is.EqualTo(1));
            Assert.That(_world.GetComponentsCount(entity), Is.EqualTo(1));
        }

        private SFEntity NewEntity(Transform parent = null, bool withComponent = false)
        {
            var gameObject = NewObject();
            if (parent != null) gameObject.transform.SetParent(parent);
            if (withComponent) gameObject.AddComponent<EcsTestComponent>().SetSerializedValue(13);
            var view = gameObject.AddComponent<SFEntity>();
            typeof(SFEntity).GetField("_worldsService", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(view, new SingleWorldService(_world));
            // Revalidate after parenting, independently of Unity editor callback timing.
            InvokeLifecycle(view, "OnValidate");
            InvokeLifecycle(view, "Init");
            return view;
        }

        private GameObject NewObject()
        {
            var gameObject = new GameObject("SFramework ECS test") { hideFlags = HideFlags.HideAndDontSave };
            gameObject.SetActive(false);
            _objects.Add(gameObject);
            return gameObject;
        }

        private static Dictionary<int, SFEcsPackedEntityWithWorld> Mappings =>
            (Dictionary<int, SFEcsPackedEntityWithWorld>)typeof(SFEntityMapping)
                .GetField("_packedEntities", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        private static void InvokeLifecycle(SFEntity view, string method)
        {
            typeof(SFEntity).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
        }

        private sealed class FailingProvider : ISFEntitySetup
        {
            public SFEcsPackedEntityWithWorld FirstEntity;

            public void Setup(ref SFEcsPackedEntityWithWorld entity)
            {
                if (FirstEntity.World != null) return;
                FirstEntity = entity;
                throw new InvalidOperationException("Broken entity setup");
            }
        }

        private sealed class SingleWorldService : ISFWorldsService
        {
            private readonly SFEcsWorld _world;
            public SingleWorldService(SFEcsWorld world) => _world = world;
            public SFEcsWorld GetWorld(string name = "") => _world;
            public SFEcsWorld[] Worlds => new[] { _world };
            public UniTask Init(CancellationToken cancellationToken) => UniTask.CompletedTask;
            public void Dispose() { }
        }
    }
}
