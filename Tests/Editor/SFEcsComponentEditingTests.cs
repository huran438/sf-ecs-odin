using NUnit.Framework;
using SFramework.ECS.Editor;
using SFramework.ECS.Runtime;

namespace SFramework.ECS.Tests
{
    public sealed class SFEcsComponentEditingTests
    {
        private struct Anchor { }

        private struct Position
        {
            public int X;
            public int Y;
        }

        private struct Probe : ISFComponent, ISFEcsAutoReset<Probe>
        {
            public Position Position;
            public int RuntimeTicks;

            public void AutoReset(ref Probe component) => component.RuntimeTicks = 17;
        }

        [Test]
        public void AddUsesPoolDefaultsAndRemoveUpdatesFilters()
        {
            var world = new SFEcsWorld();
            try
            {
                var entity = world.NewEntity();
                world.GetPool<Anchor>().Add(entity);
                var handle = world.PackEntity(entity);
                Assert.That(world.GetPoolByType(typeof(Probe)), Is.Null);
                world.GetPool<Position>();
                var addable = SFEcsComponentEditing.GetAddableTypes(world, handle);
                Assert.That(addable, Has.Member(typeof(Probe)));
                Assert.That(addable, Has.Member(typeof(Position)));
                Assert.That(addable, Has.No.Member(typeof(Anchor)));

                Assert.That(SFEcsComponentEditing.TryAdd(world, handle, typeof(Probe)), Is.True);
                Assert.That(world.GetPool<Probe>().Get(entity).RuntimeTicks, Is.EqualTo(17));
                world.GetPool<Probe>().Get(entity).RuntimeTicks = 31;
                Assert.That(SFEcsComponentEditing.TryAdd(world, handle, typeof(Probe)), Is.False);
                Assert.That(world.GetPool<Probe>().Get(entity).RuntimeTicks, Is.EqualTo(31));
                var filter = world.Filter<Probe>().End();
                Assert.That(filter.GetEntitiesCount(), Is.EqualTo(1));

                Assert.That(SFEcsComponentEditing.TryRemove(world, handle, typeof(Probe)), Is.True);
                Assert.That(filter.GetEntitiesCount(), Is.Zero);
                Assert.That(handle.Unpack(world, out _), Is.True);
                Assert.That(SFEcsComponentEditing.TryRemove(world, handle, typeof(Anchor)), Is.True);
                Assert.That(handle.Unpack(world, out _), Is.False);
            }
            finally { world.Destroy(); }
        }

        [Test]
        public void FieldEditMergesWithLatestStructAndRejectsReusedEntityId()
        {
            var world = new SFEcsWorld();
            try
            {
                var entity = world.NewEntity();
                var pool = world.GetPool<Probe>();
                ref var probe = ref pool.Add(entity);
                var handle = world.PackEntity(entity);
                var path = new[] { typeof(Probe).GetField(nameof(Probe.Position)), typeof(Position).GetField(nameof(Position.X)) };
                probe.RuntimeTicks = 42;
                probe.Position.Y = 9;

                Assert.That(SFEcsComponentEditing.TrySetField(world, handle, typeof(Probe), path, 73), Is.True);
                Assert.That(probe.Position.X, Is.EqualTo(73));
                Assert.That(probe.Position.Y, Is.EqualTo(9));
                Assert.That(probe.RuntimeTicks, Is.EqualTo(42));

                world.DelEntity(entity);
                var replacement = world.NewEntity();
                Assert.That(replacement, Is.EqualTo(entity));
                pool.Add(replacement).Position.X = 21;
                Assert.That(SFEcsComponentEditing.TrySetField(world, handle, typeof(Probe), path, 100), Is.False);
                Assert.That(SFEcsComponentEditing.TryRemove(world, handle, typeof(Probe)), Is.False);
                Assert.That(SFEcsComponentEditing.TryAdd(world, handle, typeof(Anchor)), Is.False);
                Assert.That(pool.Get(replacement).Position.X, Is.EqualTo(21));
            }
            finally { world.Destroy(); }
        }
    }
}
