using System;
using System.Collections.Generic;
using NUnit.Framework;
using SFramework.Core.Runtime;
using SFramework.ECS.Runtime;

namespace SFramework.ECS.Tests
{
    public sealed partial class SFEcsExtendedSystemsTests
    {
        private struct Event { }
        private struct Keep { }

        [Test]
        public void DelHereClearsOnlyItsComponentAtItsPositionInTheNamedWorld()
        {
            var world = new SFEcsWorld();
            var events = new SFEcsWorld();
            var systems = new SFEcsSystems(world);
            var observed = new List<int>();
            try
            {
                var entity = events.NewEntity();
                events.GetPool<Event>().Add(entity);
                events.GetPool<Keep>().Add(entity);
                world.GetPool<Event>().Add(world.NewEntity());

                systems.AddWorld(events, "events")
                    .Add(new RunAction(() => observed.Add(events.Filter<Event>().End().GetEntitiesCount())))
                    .DelHere<Event>("events")
                    .Add(new RunAction(() => observed.Add(events.Filter<Event>().End().GetEntitiesCount())));
                systems.Init();
                systems.Run();

                Assert.That(observed, Is.EqualTo(new[] { 1, 0 }));
                Assert.That(events.GetPool<Keep>().Has(entity), Is.True);
                Assert.That(world.GetEntitiesCount(), Is.EqualTo(1));
                Assert.That(world.Filter<Event>().End().GetEntitiesCount(), Is.EqualTo(1));
            }
            finally
            {
                systems.Destroy();
                events.Destroy();
                world.Destroy();
            }
        }

        [Test]
        public void NestedGroupsReceivePipelineAndCoreDependenciesBeforeAnyPreInit()
        {
            using var container = new SFServiceContainer();
            var previous = SFContainer.SetCurrent(container);
            var dependency = SFContainer.Register<Dependency>(new Dependency());
            var world = new SFEcsWorld();
            var events = new SFEcsWorld();
            var shared = new object();
            var systems = new SFEcsSystems(world, shared);
            var child = new ChildSystem();
            try
            {
                systems.AddWorld(events, "events")
                    .Add(new PreInitAction(() =>
                    {
                        Assert.That(child.Injections, Is.EqualTo(1));
                        Assert.That(child.Service, Is.SameAs(dependency));
                        Assert.That(child.Ecs.GetWorld(), Is.SameAs(world));
                        Assert.That(child.Ecs.GetPool<Event>("events"), Is.SameAs(events.GetPool<Event>()));
                        Assert.That(child.Ecs.Filter<Keep>().End(), Is.SameAs(world.Filter<Keep>().End()));
                        Assert.That(child.Ecs.GetShared<object>(), Is.SameAs(shared));
                    }))
                    .AddGroup("outer", false, "events", new SFEcsGroupSystem("inner", true, "events", child));
                systems.Init();
                systems.Run();
                Assert.That(child.Lifecycle, Is.EqualTo(new[] { "preinit", "init" }));

                ChangeGroup(events, "inner", false);
                ChangeGroup(events, "outer", true);
                systems.Run();
                Assert.That(child.Lifecycle.Count, Is.EqualTo(2));

                ChangeGroup(events, "inner", true);
                systems.Run();
                Assert.That(events.GetEntitiesCount(), Is.Zero);
                Assert.That(child.Lifecycle, Is.EqualTo(new[] { "preinit", "init", "run", "postrun" }));

                ChangeGroup(events, "outer", false);
                systems.Run();
                systems.Destroy();
                Assert.That(child.Lifecycle, Is.EqualTo(new[] { "preinit", "init", "run", "postrun", "destroy", "postdestroy" }));
            }
            finally
            {
                systems.Destroy();
                events.Destroy();
                world.Destroy();
                SFContainer.SetCurrent(previous);
            }
        }

        private static void ChangeGroup(SFEcsWorld events, string name, bool enabled)
        {
            events.GetPool<SFEcsGroupSystemState>().Add(events.NewEntity()) =
                new SFEcsGroupSystemState { Name = name, State = enabled };
        }

        private sealed class RunAction : ISFEcsRunSystem
        {
            private readonly Action _action;
            public RunAction(Action action) => _action = action;
            public void Run(ISFEcsSystems systems) => _action();
        }

        private sealed class PreInitAction : ISFEcsPreInitSystem
        {
            private readonly Action _action;
            public PreInitAction(Action action) => _action = action;
            public void PreInit(ISFEcsSystems systems) => _action();
        }

        private sealed class Dependency : ISFDependency { }

        private sealed partial class ChildSystem : ISFInjectable, ISFInjectionCallback, ISFEcsPreInitSystem,
            ISFEcsInitSystem, ISFEcsRunSystem, ISFEcsPostRunSystem, ISFEcsDestroySystem, ISFEcsPostDestroySystem
        {
            private ISFEcsContext _ecs;
            private Dependency _service;

            public ISFEcsContext Ecs => _ecs;
            public Dependency Service => _service;
            public int Injections { get; private set; }
            public List<string> Lifecycle { get; } = new();

            public void OnDependenciesInjected() => Injections++;
            public void PreInit(ISFEcsSystems systems) => Lifecycle.Add("preinit");
            public void Init(ISFEcsSystems systems) => Lifecycle.Add("init");
            public void Run(ISFEcsSystems systems) => Lifecycle.Add("run");
            public void PostRun(ISFEcsSystems systems) => Lifecycle.Add("postrun");
            public void Destroy(ISFEcsSystems systems) => Lifecycle.Add("destroy");
            public void PostDestroy(ISFEcsSystems systems) => Lifecycle.Add("postdestroy");
        }
    }
}
