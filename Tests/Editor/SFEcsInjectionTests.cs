using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SFramework.Core.Runtime;
using SFramework.ECS.Runtime;

namespace SFramework.ECS.Tests
{
    public sealed partial class SFEcsInjectionTests
    {
        private ContainerState _container;
        private SFEcsWorld _world;
        private SFEcsSystems _systems;

        [SetUp]
        public void SetUp()
        {
            _container = new ContainerState();
            _world = new SFEcsWorld();
            _systems = new SFEcsSystems(_world);
        }

        [TearDown]
        public void TearDown()
        {
            _systems.Destroy();
            _world.Destroy();
            _container.Dispose();
        }

        [Test]
        public void EcsRuntimeCanBeUsedWithoutAnyLeopotamAssembly()
        {
            Assert.That(typeof(SFEcsWorld).Assembly, Is.SameAs(typeof(SFEntity).Assembly));
            foreach (var reference in typeof(SFEntity).Assembly.GetReferencedAssemblies())
                Assert.That(reference.Name, Does.Not.StartWith("Leopotam."));
        }

        [Test]
        public void PlainSystemsReceiveAutomaticServiceFieldsBeforeAnyPreInit()
        {
            var dependency = SFContainer.Register<Dependency>(new Dependency());
            var consumer = new PlainSystem();
            _systems.Add(new PreInitObserver(consumer, dependency));
            _systems.Add(consumer);

            _systems.Init();
            _systems.Run();

            Assert.That(consumer.Field, Is.SameAs(dependency));
            Assert.That(consumer.Property, Is.SameAs(dependency));
            Assert.That(consumer.Argument, Is.SameAs(dependency));
            Assert.That(consumer.Injections, Is.EqualTo(1));
            Assert.That(consumer.RunCount, Is.EqualTo(1));
        }

        [Test]
        public void SFSystemReceivesCoreInjectionOnceBeforeInitAndCanAccessItsWorld()
        {
            var dependency = SFContainer.Register<Dependency>(new Dependency());
            var system = new DerivedSystem();
            _systems.Add(system);

            _systems.Init();
            _systems.Run();

            Assert.That(system.Dependency, Is.SameAs(dependency));
            Assert.That(system.Injections, Is.EqualTo(1));
            Assert.That(system.RunWorld, Is.SameAs(_world));
        }

        public sealed class Dependency : ISFDependency { }

        private sealed partial class PlainSystem : ISFInjectable, ISFInjectionCallback, ISFEcsInitSystem, ISFEcsRunSystem
        {
            private Dependency _field;
            private Dependency _property;
            private Dependency _argument;
            public Dependency Property => _property;
            public Dependency Field => _field;
            public Dependency Argument => _argument;
            public int Injections { get; private set; }
            public int RunCount { get; private set; }

            public void OnDependenciesInjected()
            {
                Injections++;
            }

            public void Init(ISFEcsSystems systems)
            {
                Assert.That(_field, Is.Not.Null);
                Assert.That(Property, Is.SameAs(_field));
                Assert.That(Argument, Is.SameAs(_field));
            }

            public void Run(ISFEcsSystems systems) => RunCount++;
        }

        private sealed class PreInitObserver : ISFEcsPreInitSystem
        {
            private readonly PlainSystem _consumer;
            private readonly Dependency _dependency;
            public PreInitObserver(PlainSystem consumer, Dependency dependency)
            { _consumer = consumer; _dependency = dependency; }
            public void PreInit(ISFEcsSystems systems)
            {
                Assert.That(_consumer.Field, Is.SameAs(_dependency));
                Assert.That(_consumer.Property, Is.SameAs(_dependency));
                Assert.That(_consumer.Argument, Is.SameAs(_dependency));
            }
        }

        private sealed partial class DerivedSystem : SFSystem, ISFInjectionCallback, ISFEcsInitSystem
        {
            private Dependency _dependency;
            public Dependency Dependency => _dependency;
            public int Injections { get; private set; }
            public SFEcsWorld RunWorld { get; private set; }
            public void OnDependenciesInjected() => Injections++;
            public void Init(ISFEcsSystems systems)
            { Assert.That(Injections, Is.EqualTo(1)); }
            protected override void Tick(ref ISFEcsSystems systems) => RunWorld = systems.GetWorld();
        }

        // Restore the editor's default container after each test.
        private sealed class ContainerState : IDisposable
        {
            private readonly SFServiceContainer _previous = SFContainer.Current;
            private readonly SFServiceContainer _isolated = new();
            public ContainerState() { InitializeEmpty(); }
            public void InitializeEmpty() => SFContainer.SetCurrent(_isolated);
            public void Dispose()
            {
                SFContainer.SetCurrent(_previous);
                _isolated.Dispose();
            }
        }
    }
}
