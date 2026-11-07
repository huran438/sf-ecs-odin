using System.Collections.Generic;
using NUnit.Framework;
using SFramework.ECS.Runtime;

namespace SFramework.ECS.Tests
{
    public sealed class SFEcsRuntimeTests
    {
        private SFEcsWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new SFEcsWorld(new SFEcsWorld.Config
            {
                Entities = 2, RecycledEntities = 2, Pools = 2, Filters = 2,
                PoolDenseSize = 2, PoolRecycledSize = 2, EntityComponentsSize = 2
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (_world.IsAlive()) _world.Destroy();
        }

        [Test]
        public void PoolsGrowAndRecycleWhileOldPackedHandlesStayInvalid()
        {
            var pool = _world.GetPool<Position>();
            var first = _world.NewEntity();
            pool.Add(first).Value = 99;
            var packed = _world.PackEntity(first);
            var packedWithWorld = _world.PackEntityWithWorld(first);
            for (var i = 1; i < 64; i++) pool.Add(_world.NewEntity()).Value = i;
            Assert.That(_world.Filter<Position>().End().GetEntitiesCount(), Is.EqualTo(64));

            // Removing the final component destroys the entity and recycles its ID.
            pool.Del(first);
            var recycled = _world.NewEntity();
            Assert.That(recycled, Is.EqualTo(first));
            Assert.That(pool.Add(recycled).Value, Is.Zero);
            Assert.That(packed.Unpack(_world, out _), Is.False);
            Assert.That(packedWithWorld.Unpack(out _, out _), Is.False);
            Assert.That(_world.PackEntity(recycled).Unpack(_world, out _), Is.True);
            Assert.That(_world.Filter<Position>().End().GetEntitiesCount(), Is.EqualTo(64));
            _world.Destroy();
            Assert.That(packedWithWorld.Unpack(out _, out _), Is.False);
        }

        [Test]
        public void EntityCopyUsesAutoCopyAndRecycledComponentsUseAutoReset()
        {
            var pool = _world.GetPool<Inventory>();
            var source = _world.NewEntity();
            pool.Add(source).Items.Add(7);
            var destination = _world.NewEntity();

            _world.CopyEntity(source, destination);

            Assert.That(pool.Get(destination).Items, Is.EqualTo(new[] { 7 }));
            Assert.That(pool.Get(destination).Items, Is.Not.SameAs(pool.Get(source).Items));
            pool.Get(source).Items.Add(9);
            Assert.That(pool.Get(destination).Items, Is.EqualTo(new[] { 7 }));

            pool.Del(destination);
            var recycled = _world.NewEntity();
            Assert.That(pool.Add(recycled).Items, Is.Empty);
        }

        [Test]
        public void FiltersApplyIncludesExcludesAndDeferMutationsUntilEnumerationEnds()
        {
            var positions = _world.GetPool<Position>();
            var disabled = _world.GetPool<Disabled>();
            for (var i = 0; i < 12; i++)
            {
                var entity = _world.NewEntity();
                positions.Add(entity).Value = i;
                if (i % 2 == 0) disabled.Add(entity);
            }
            var active = _world.Filter<Position>().Exc<Disabled>().End();
            var visited = new List<int>();

            foreach (var entity in active)
            {
                visited.Add(positions.Get(entity).Value);
                disabled.Add(entity);
            }

            Assert.That(visited, Is.EquivalentTo(new[] { 1, 3, 5, 7, 9, 11 }));
            Assert.That(active.GetEntitiesCount(), Is.Zero);
            foreach (var entity in _world.Filter<Position>().Inc<Disabled>().End())
                disabled.Del(entity);
            Assert.That(active.GetEntitiesCount(), Is.EqualTo(12));
        }

        [Test]
        public void PipelinesPreserveSharedDataNamedWorldsPhaseOrderAndReverseTeardown()
        {
            var named = new SFEcsWorld();
            var events = new List<string>();
            var systems = new SFEcsSystems(_world, events);
            try
            {
                systems.AddWorld(named, "other");
                systems.Add(new LifecycleProbe("A"));
                systems.Add(new LifecycleProbe("B"));
                Assert.That(systems.GetWorld(), Is.SameAs(_world));
                Assert.That(systems.GetWorld("other"), Is.SameAs(named));
                Assert.That(systems.GetWorld("missing"), Is.Null);

                systems.Init();
                systems.Run();
                systems.Destroy();

                Assert.That(events, Is.EqualTo(new[]
                {
                    "pre:A", "pre:B", "init:A", "init:B", "run:A", "run:B",
                    "post:A", "post:B", "destroy:B", "destroy:A", "after:B", "after:A"
                }));
                Assert.That(named.IsAlive(), Is.True);
                Assert.That(systems.GetAllSystems(), Is.Empty);
                Assert.That(systems.GetAllNamedWorlds(), Is.Empty);
            }
            finally
            {
                if (systems.GetAllSystems().Count > 0) systems.Destroy();
                named.Destroy();
            }
        }

        public struct Position { public int Value; }
        public struct Disabled { }

        public struct Inventory : ISFEcsAutoReset<Inventory>, ISFEcsAutoCopy<Inventory>
        {
            public List<int> Items;
            public void AutoReset(ref Inventory component)
            {
                if (component.Items == null) component.Items = new List<int>();
                else component.Items.Clear();
            }
            public void AutoCopy(ref Inventory source, ref Inventory destination)
            {
                destination.Items.Clear();
                destination.Items.AddRange(source.Items);
            }
        }

        private sealed class LifecycleProbe : ISFEcsPreInitSystem, ISFEcsInitSystem, ISFEcsRunSystem,
            ISFEcsPostRunSystem, ISFEcsDestroySystem, ISFEcsPostDestroySystem
        {
            private readonly string _name;
            public LifecycleProbe(string name) => _name = name;
            public void PreInit(ISFEcsSystems systems) => Record(systems, "pre");
            public void Init(ISFEcsSystems systems) => Record(systems, "init");
            public void Run(ISFEcsSystems systems) => Record(systems, "run");
            public void PostRun(ISFEcsSystems systems) => Record(systems, "post");
            public void Destroy(ISFEcsSystems systems) => Record(systems, "destroy");
            public void PostDestroy(ISFEcsSystems systems) => Record(systems, "after");
            private void Record(ISFEcsSystems systems, string phase) =>
                systems.GetShared<List<string>>().Add(phase + ":" + _name);
        }
    }
}
