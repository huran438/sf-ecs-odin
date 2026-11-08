using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using SFramework.Configs.Runtime;
using SFramework.Core.Runtime;
using SFramework.ECS.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SFramework.ECS.Tests
{
    public sealed partial class SFWorldsServiceTests
    {
        private SFWorldsService _service;
        private SFECSCallbacks _callbacks;

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
        }

        [Test]
        public void ConfiguredWorldsRouteCallbacksToTheirOwnEnabledSystemsAndStopAfterDispose()
        {
            var first = Node("first");
            var second = Node("second");
            // Duplicate IDs must be rejected before attempting to inspect their data.
            var duplicate = new SFWorldNode { Id = "first" };
            CreateService(new SFWorldsConfig { Id = "Gameplay", Worlds = new[] { first, second, duplicate } });
            Initialize();
            var firstWorld = _service.GetWorld("first");
            var secondWorld = _service.GetWorld("second");
            Assert.That(_service.GetWorld("Gameplay/first"), Is.SameAs(firstWorld));

            Initialize();

            RaiseCallback("FixedUpdate");
            RaiseCallback("Update");
            RaiseCallback("Update");
            RaiseCallback("LateUpdate");

            AssertCounts(firstWorld, 1, 2, 1);
            AssertCounts(secondWorld, 1, 2, 1);
            Assert.That(firstWorld, Is.Not.SameAs(secondWorld));
            Assert.That(_service.Worlds, Has.Length.EqualTo(2));
            Assert.That(_service.GetWorld().Filter<TickCounts>().End().GetEntitiesCount(), Is.Zero);

            var fallbackWorld = _service.GetWorld();
            _service.Dispose();
            _service.Dispose();

            Assert.That(_callbacks == null, Is.True);
            Assert.That(firstWorld.IsAlive(), Is.False);
            Assert.That(secondWorld.IsAlive(), Is.False);
            Assert.That(fallbackWorld.IsAlive(), Is.False);
            Assert.That(_service.Worlds, Is.Empty);
        }

        [Test]
        public void ConfiguredGroupsInjectContextAndCanClearEventsInAWorldDeclaredLater()
        {
            var simulation = new SFWorldNode
            {
                Id = "Simulation",
                UpdateSystems = new[] { Entry<NamedWorldGroup>(), Entry<NamedWorldCleanup>() }
            };
            var events = new SFWorldNode { Id = "Events" };
            CreateService(new SFWorldsConfig { Id = "Gameplay", Worlds = new[] { simulation, events } });
            Initialize();

            var world = _service.GetWorld("Gameplay/Events");
            Assert.That(world.Filter<TickCounts>().End().GetEntitiesCount(), Is.EqualTo(1));
            Assert.That(_service.GetWorld("Gameplay/Simulation").GetEntitiesCount(), Is.Zero);

            RaiseCallback("Update");
            Assert.That(world.GetEntitiesCount(), Is.Zero);
        }

        [Test]
        public void QualifiedIdsDistinguishWorldsWithTheSameShortId()
        {
            CreateService(
                new SFWorldsConfig { Id = "Gameplay", Worlds = new[] { new SFWorldNode { Id = "Shared" } } },
                new SFWorldsConfig { Id = "Preview", Worlds = new[] { new SFWorldNode { Id = "Shared" } } },
                new SFWorldsConfig { Id = "Empty", Worlds = null });
            Initialize();

            var gameplay = _service.GetWorld("Gameplay/Shared");
            var preview = _service.GetWorld("Preview/Shared");
            Assert.That(_service.Worlds, Is.EquivalentTo(new[] { gameplay, preview }));
            Assert.That(gameplay, Is.Not.SameAs(preview));
            Assert.That(_service.GetWorld("Shared"), Is.SameAs(_service.GetWorld()));
        }

        [Test]
        public void EnabledNamedEntityWaitsForItsConfiguredWorldAndLosesItsMappingOnShutdown()
        {
            var config = new SFWorldsConfig { Id = "Gameplay", Worlds = new[] { new SFWorldNode { Id = "Actors" } } };
            var builder = new SFContainerBuilder();
            builder.RegisterInstance<ISFConfigsService>(new InMemoryConfigs(new[] { config }));
            builder.Register<ISFWorldsService, SFWorldsService>();
            using var container = builder.Build();
            _service = (SFWorldsService)container.Resolve<ISFWorldsService>();
            var gameObject = new GameObject("ECS named entity") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var entity = gameObject.AddComponent<SFEntity>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(SFEntity).GetField("_world", flags).SetValue(entity, "Gameplay/Actors");
                container.Inject(entity);
                typeof(SFEntity).GetMethod("OnEnable", flags).Invoke(entity, null);
                Assert.That(SFEntityMapping.GetEntityPacked(gameObject, out _), Is.False);

                Initialize();
                Assert.That(SFEntityMapping.GetEntity(gameObject, out var world, out _), Is.True);
                Assert.That(world, Is.SameAs(_service.GetWorld("Gameplay/Actors")));

                _service.Dispose();
                Assert.That(SFEntityMapping.GetEntityPacked(gameObject, out _), Is.False);
            }
            finally { Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void FailedInitializationDestroysStartedPipelinesAndAllowsRetry()
        {
            LifecycleProbe.Events.Clear();
            var node = Node("first");
            node.FixedUpdateSystems = new[] { Entry<LifecycleProbe>() };
            node.UpdateSystems = new[] { Entry<ThrowOnInit>() };
            CreateService(new SFWorldsConfig { Worlds = new[] { node } });

            Assert.Throws<InvalidOperationException>(Initialize);
            Assert.That(_service.Worlds, Is.Empty);
            CollectionAssert.AreEqual(new[] { "init", "destroy", "post-destroy" }, LifecycleProbe.Events);

            LifecycleProbe.Events.Clear();
            node.UpdateSystems = null;
            Initialize();
            Assert.That(_service.GetWorld("first").IsAlive(), Is.True);
            _service.Dispose();
            CollectionAssert.AreEqual(new[] { "init", "destroy", "post-destroy" }, LifecycleProbe.Events);
        }

        [Test]
        public void MissingAndEmptyWorldNamesShareTheDefaultWorldForEntityOperations()
        {
            CreateService();
            Initialize();
            var entity = _service.GetWorld("unconfigured").NewEntity();
            _service.GetWorld("unconfigured").GetPool<TickCounts>().Add(entity).Update = 17;

            Assert.That(_service.GetWorld().GetPool<TickCounts>().Get(entity).Update, Is.EqualTo(17));
            Assert.That(_service.GetWorld(null).GetPool<TickCounts>().Get(entity).Update, Is.EqualTo(17));
            Assert.That(_service.GetWorld(string.Empty).Filter<TickCounts>().End().GetEntitiesCount(), Is.EqualTo(1));
        }

        private void CreateService(params SFWorldsConfig[] configs)
        {
            _service = (SFWorldsService)Activator.CreateInstance(typeof(SFWorldsService),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { new InMemoryConfigs(configs) }, null);
        }

        private void Initialize()
        {
            _service.Init(CancellationToken.None).GetAwaiter().GetResult();
            _callbacks = (SFECSCallbacks)typeof(SFWorldsService)
                .GetField("_callbacks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_service);
            _callbacks.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        private void RaiseCallback(string method)
        {
            typeof(SFECSCallbacks).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_callbacks, null);
        }

        private static SFWorldNode Node(string id)
        {
            return new SFWorldNode
            {
                Id = id,
                Config = new SFWorldConfig(),
                FixedUpdateSystems = new[] { Entry<InitializeCounts>(), Entry<FixedTick>() },
                UpdateSystems = new[]
                {
                    Entry<UpdateTick>(),
                    Entry<MustStayDisabled>(false),
                    new SFSystemContainer { System = "Missing.System, Missing.Assembly" },
                    new SFSystemContainer { System = typeof(object).AssemblyQualifiedName }
                },
                LateUpdateSystems = new[] { Entry<LateTick>() }
            };
        }

        private static SFSystemContainer Entry<T>(bool enabled = true) =>
            new() { Enabled = enabled, System = typeof(T).AssemblyQualifiedName };

        private static void AssertCounts(SFEcsWorld world, int fixedCount, int updateCount, int lateCount)
        {
            var filter = world.Filter<TickCounts>().End();
            Assert.That(filter.GetEntitiesCount(), Is.EqualTo(1));
            foreach (var entity in filter)
            {
                var counts = world.GetPool<TickCounts>().Get(entity);
                Assert.That(counts.Fixed, Is.EqualTo(fixedCount));
                Assert.That(counts.Update, Is.EqualTo(updateCount));
                Assert.That(counts.Late, Is.EqualTo(lateCount));
            }
        }

        public struct TickCounts
        {
            public int Fixed;
            public int Update;
            public int Late;
        }

        public sealed class InitializeCounts : ISFEcsInitSystem
        {
            public void Init(ISFEcsSystems systems)
            {
                var world = systems.GetWorld();
                world.GetPool<TickCounts>().Add(world.NewEntity());
            }
        }

        public sealed partial class NamedWorldProbe : SFSystem, ISFInjectionCallback
        {
            private ISFEcsContext _ecs;

            public void OnDependenciesInjected()
            {
                var world = _ecs.GetWorld("Gameplay/Events");
                _ecs.GetPool<TickCounts>("Gameplay/Events").Add(world.NewEntity());
            }
        }

        public sealed class NamedWorldGroup : SFEcsGroupSystem
        {
            public NamedWorldGroup() : base("Probe", true, "Gameplay/Events", new NamedWorldProbe()) { }
        }

        public sealed class NamedWorldCleanup : SFEcsDelHereSystem<TickCounts>
        {
            public NamedWorldCleanup() : base("Gameplay/Events") { }
        }

        public sealed class FixedTick : ISFEcsRunSystem
        {
            public void Run(ISFEcsSystems systems)
            {
                var world = systems.GetWorld();
                foreach (var entity in world.Filter<TickCounts>().End())
                    world.GetPool<TickCounts>().Get(entity).Fixed++;
            }
        }

        public sealed class UpdateTick : ISFEcsRunSystem
        {
            public void Run(ISFEcsSystems systems)
            {
                var world = systems.GetWorld();
                foreach (var entity in world.Filter<TickCounts>().End())
                    world.GetPool<TickCounts>().Get(entity).Update++;
            }
        }

        public sealed class LateTick : ISFEcsRunSystem
        {
            public void Run(ISFEcsSystems systems)
            {
                var world = systems.GetWorld();
                foreach (var entity in world.Filter<TickCounts>().End())
                    world.GetPool<TickCounts>().Get(entity).Late++;
            }
        }

        public sealed class MustStayDisabled : ISFEcsInitSystem
        {
            public void Init(ISFEcsSystems systems) => Assert.Fail("A disabled config entry was instantiated.");
        }

        public sealed class LifecycleProbe : ISFEcsInitSystem, ISFEcsDestroySystem, ISFEcsPostDestroySystem
        {
            public static readonly List<string> Events = new();
            public void Init(ISFEcsSystems systems) => Events.Add("init");

            public void Destroy(ISFEcsSystems systems)
            {
                Assert.That(systems.GetWorld().IsAlive(), Is.True);
                Events.Add("destroy");
            }

            public void PostDestroy(ISFEcsSystems systems) => Events.Add("post-destroy");
        }

        public sealed class ThrowOnInit : ISFEcsInitSystem
        {
            public void Init(ISFEcsSystems systems)
            {
                systems.GetWorld().NewEntity();
                throw new InvalidOperationException("Broken system setup");
            }
        }

        private sealed class InMemoryConfigs : ISFConfigsService
        {
            private readonly SFWorldsConfig[] _configs;
            public InMemoryConfigs(SFWorldsConfig[] configs) => _configs = configs;
            public IEnumerable<ISFConfig> Configs => _configs;
            public bool TryGetConfigs<T>(out T[] configs) where T : ISFConfig
            {
                configs = typeof(T) == typeof(SFWorldsConfig) ? (T[])(object)_configs : Array.Empty<T>();
                return configs.Length > 0;
            }
            public bool TryGetGlobalConfig<T>(out T config) where T : ISFGlobalConfig
            {
                config = default;
                return false;
            }
            public IEnumerable<T> GetConfigs<T>() where T : ISFConfig
            {
                TryGetConfigs(out T[] configs);
                return configs;
            }
            public UniTask Init(CancellationToken cancellationToken) => UniTask.CompletedTask;
            public void Dispose() { }
        }
    }
}
