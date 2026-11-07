using System.Reflection;
using NUnit.Framework;
using SFramework.ECS.Editor;
using SFramework.ECS.Runtime;
using UnityEngine;

public struct GlobalComponentForGeneratorTest { }

namespace SFramework.ECS.Tests
{
    public sealed class SFSystemsTests
    {
        private SFEcsWorld _world;
        private SFEcsSystems _systems;

        [SetUp]
        public void SetUp()
        {
            _world = new SFEcsWorld();
            _systems = new SFEcsSystems(_world);
        }

        [TearDown]
        public void TearDown()
        {
            _systems.Destroy();
            if (_world.IsAlive()) _world.Destroy();
        }

        [Test]
        public void AddRunsStandaloneAndGroupedSystemsInOrderAndSkipsNonEcsEntries()
        {
            var entity = _world.NewEntity();
            _world.GetPool<ExecutionTrace>().Add(entity);
            SFExtensions.Add(_systems, new ISFSystem[]
            {
                new AppendDigitSystem(1),
                new TestGroup(new NonEcsEntry(), new AppendDigitSystem(2), new AppendDigitSystem(3)),
                new NonEcsEntry()
            });
            _systems.Init();

            _systems.Run();
            Assert.That(_world.GetPool<ExecutionTrace>().Get(entity).Value, Is.EqualTo(123));
            _systems.Run();
            Assert.That(_world.GetPool<ExecutionTrace>().Get(entity).Value, Is.EqualTo(123123));
        }

        [Test]
        public void SFSystemRunPassesTheActiveSystemsWorldToTick()
        {
            var entity = _world.NewEntity();
            _world.GetPool<ExecutionTrace>().Add(entity).Value = 6;
            _systems.Add(new TickSystem());
            _systems.Init();

            _systems.Run();

            Assert.That(_world.GetPool<ExecutionTrace>().Get(entity).Value, Is.EqualTo(42));
        }

        [Test]
        public void ProviderSourceSupportsAComponentInTheGlobalNamespace()
        {
            var generate = typeof(SFComponentsGenerator).GetMethod("GenerateProviderSource", BindingFlags.NonPublic | BindingFlags.Static);
            var source = (string)generate.Invoke(null, new object[] { typeof(GlobalComponentForGeneratorTest) });

            StringAssert.Contains("SFComponent<global::GlobalComponentForGeneratorTest>", source);
            StringAssert.DoesNotContain("namespace", source);
            StringAssert.DoesNotContain("IL2CPP_OPTIMIZATIONS", source);
        }

        [Test]
        public void SerializableTypePreservesAnInspectorEditToAnUnresolvedTypeName()
        {
            const string missingName = "Old.Namespace.Component, Old.Assembly";
            SFSerializableType type = typeof(ExecutionTrace);
            typeof(SFSerializableType).GetField("assemblyQualifiedName", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(type, missingName);

            StringAssert.Contains(missingName, JsonUtility.ToJson(type));
            Assert.That(type.Type, Is.Null);
        }

        public struct ExecutionTrace { public int Value; }

        private sealed class AppendDigitSystem : ISFSystem, ISFEcsRunSystem
        {
            private readonly int _digit;
            public AppendDigitSystem(int digit) => _digit = digit;
            public void Run(ISFEcsSystems systems)
            {
                var world = systems.GetWorld();
                var pool = world.GetPool<ExecutionTrace>();
                foreach (var entity in world.Filter<ExecutionTrace>().End())
                    pool.Get(entity).Value = pool.Get(entity).Value * 10 + _digit;
            }
        }

        private sealed class TickSystem : SFSystem
        {
            protected override void Tick(ref ISFEcsSystems systems)
            {
                var world = systems.GetWorld();
                foreach (var entity in world.Filter<ExecutionTrace>().End())
                    world.GetPool<ExecutionTrace>().Get(entity).Value *= 7;
            }
        }

        private sealed class NonEcsEntry : ISFSystem { }

        private sealed class TestGroup : SFSystemsGroupBase
        {
            private readonly ISFSystem[] _systems;
            public TestGroup(params ISFSystem[] systems) => _systems = systems;
            public override ISFSystem[] Systems => _systems;
        }
    }
}
