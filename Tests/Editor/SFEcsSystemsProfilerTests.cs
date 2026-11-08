using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using SFramework.ECS.Runtime;

namespace SFramework.ECS.Tests
{
    public sealed class SFEcsSystemsProfilerTests
    {
        [Test]
        public void RecordsEachCallbackSlotOnlyWhileRecording()
        {
            var calls = new List<string>();
            var world = new SFEcsWorld();
            var systems = new SFEcsSystems(world);
            var shared = new Probe(() => calls.Add("run"), () => calls.Add("post"));
            using var recording = new SFEcsSystemsProfiler.Recording();
            try
            {
                systems.Add(shared).Add(new InitOnly()).Add(shared);
                systems.Init();
                var handles = new List<SFEcsSystemsProfiler.PipelineHandle>();
                SFEcsSystemsProfiler.GetLivePipelines(handles);
                var handle = handles.Single(candidate => candidate.TryGetPipeline(out var live) && ReferenceEquals(live, systems));
                Assert.That(handle.Phase, Is.EqualTo("Manual"));
                Assert.That(handle.WorldName, Is.EqualTo(world.DebugName));
                systems.Run();

                recording.Resume();
                systems.Run();
                systems.Run();
                recording.Pause();
                systems.Run();

                var slots = SFEcsSystemsProfiler.GetSystems(systems);
                Assert.That(slots.Count, Is.EqualTo(3));
                Assert.That(slots[1].HasRun || slots[1].HasPostRun, Is.False);
                foreach (var slot in new[] { slots[0], slots[2] })
                {
                    AssertSample(recording.GetSample(slot, postRun: false), 2);
                    AssertSample(recording.GetSample(slot, postRun: true), 2);
                }
                Assert.That(calls, Is.EqualTo(Enumerable.Repeat(new[] { "run", "run", "post", "post" }, 4)
                    .SelectMany(batch => batch)));

                recording.Clear();
                Assert.That(recording.GetSample(slots[0], postRun: false).Count, Is.Zero);
                systems.Destroy();
                Assert.That(handle.TryGetPipeline(out _), Is.False);
            }
            finally
            {
                systems.Destroy();
                world.Destroy();
            }
        }

        [Test]
        public void DisabledGroupChecksStateWithoutTimingItsDescendants()
        {
            var world = new SFEcsWorld();
            var systems = new SFEcsSystems(world);
            var probe = new Probe(() =>
            {
                var clock = Stopwatch.StartNew();
                while (clock.ElapsedMilliseconds < 1) { }
            });
            var inner = new SFEcsGroupSystem("Inner", true, null, probe);
            using var recording = new SFEcsSystemsProfiler.Recording();
            try
            {
                systems.Add(probe).AddGroup("Outer", false, null, inner);
                systems.Init();
                var slots = SFEcsSystemsProfiler.GetSystems(systems);
                var outerSlot = slots[1];
                var innerSlot = outerSlot.Children[0];
                var child = innerSlot.Children[0];

                recording.Resume();
                systems.Run();
                Assert.That(outerSlot.GroupEnabled, Is.False);
                Assert.That(innerSlot.GroupEnabled, Is.True);
                Assert.That(recording.GetSample(outerSlot, postRun: false).Count, Is.EqualTo(1));
                Assert.That(recording.GetSample(innerSlot, postRun: false).Count, Is.Zero);
                Assert.That(recording.GetSample(child, postRun: true).Count, Is.Zero);

                world.GetPool<SFEcsGroupSystemState>().Add(world.NewEntity()) =
                    new SFEcsGroupSystemState { Name = "Outer", State = true };
                systems.Run();

                Assert.That(outerSlot.GroupEnabled, Is.True);
                foreach (var postRun in new[] { false, true })
                {
                    var outerSample = recording.GetSample(outerSlot, postRun);
                    var innerSample = recording.GetSample(innerSlot, postRun);
                    var childSample = recording.GetSample(child, postRun);
                    AssertSample(recording.GetSample(slots[0], postRun), 2);
                    AssertSample(childSample, 1);
                    if (!postRun) Assert.That(childSample.LastMilliseconds, Is.GreaterThanOrEqualTo(1));
                    Assert.That(outerSample.LastMilliseconds, Is.GreaterThanOrEqualTo(innerSample.LastMilliseconds));
                    Assert.That(innerSample.LastMilliseconds, Is.GreaterThanOrEqualTo(childSample.LastMilliseconds));
                }
            }
            finally
            {
                systems.Destroy();
                world.Destroy();
            }
        }

        [Test]
        public void CallbackExceptionKeepsItsOriginalIdentityAndStopsLaterCallbacks()
        {
            var world = new SFEcsWorld();
            var systems = new SFEcsSystems(world);
            var failure = new InvalidOperationException("broken callback");
            var shouldThrow = true;
            var probe = new Probe(() => { if (shouldThrow) throw failure; });
            using var recording = new SFEcsSystemsProfiler.Recording();
            try
            {
                systems.AddGroup("Group", true, null, probe).Add(new Probe());
                systems.Init();
                var slots = SFEcsSystemsProfiler.GetSystems(systems);
                var group = slots[0];
                var child = group.Children[0];

                recording.Resume();
                Assert.That(Assert.Throws<InvalidOperationException>(systems.Run), Is.SameAs(failure));
                Assert.That(recording.GetSample(child, postRun: false).Count, Is.EqualTo(1));
                Assert.That(recording.GetSample(child, postRun: true).Count, Is.Zero);
                Assert.That(recording.GetSample(slots[1], postRun: false).Count, Is.Zero);

                shouldThrow = false;
                systems.Run();
                Assert.That(recording.GetSample(child, postRun: false).Count, Is.EqualTo(2));
                Assert.That(recording.GetSample(child, postRun: true).Count, Is.EqualTo(1));
                Assert.That(recording.GetSample(slots[1], postRun: false).Count, Is.EqualTo(1));
            }
            finally
            {
                systems.Destroy();
                world.Destroy();
            }
        }

        private static void AssertSample(SFEcsSystemsProfiler.Sample sample, long count)
        {
            Assert.That(sample.Count, Is.EqualTo(count));
            Assert.That(sample.LastMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(sample.AverageMilliseconds, Is.InRange(0d, sample.PeakMilliseconds));
            Assert.That(sample.PeakMilliseconds, Is.GreaterThanOrEqualTo(sample.LastMilliseconds));
        }

        private sealed class Probe : ISFEcsRunSystem, ISFEcsPostRunSystem
        {
            private readonly Action _run;
            private readonly Action _postRun;

            public Probe(Action run = null, Action postRun = null)
            {
                _run = run;
                _postRun = postRun;
            }

            public void Run(ISFEcsSystems systems) => _run?.Invoke();
            public void PostRun(ISFEcsSystems systems) => _postRun?.Invoke();
        }

        private sealed class InitOnly : ISFEcsInitSystem
        {
            public void Init(ISFEcsSystems systems) { }
        }
    }

    public sealed partial class SFWorldsServiceTests
    {
        [Test]
        public void ProfilerSeparatesConfiguredPhasesAndUnregistersThemOnDisposal()
        {
            const string worldId = "Profiler/Simulation";
            var entries = new[] { Entry<UpdateTick>() };
            CreateService(new SFWorldsConfig
            {
                Id = "Profiler",
                Worlds = new[] { new SFWorldNode { Id = "Simulation", FixedUpdateSystems = entries,
                    UpdateSystems = entries, LateUpdateSystems = entries } }
            });
            Initialize();
            var handles = new List<SFEcsSystemsProfiler.PipelineHandle>();
            SFEcsSystemsProfiler.GetLivePipelines(handles);
            var phases = handles.Where(handle => handle.WorldName == worldId).ToDictionary(handle => handle.Phase);
            Assert.That(phases.Keys, Is.EquivalentTo(new[] { "FixedUpdate", "Update", "LateUpdate" }));

            using var recording = new SFEcsSystemsProfiler.Recording();
            recording.Resume();
            RaiseCallback("FixedUpdate");
            RaiseCallback("Update");
            RaiseCallback("Update");

            long Count(string phase)
            {
                Assert.That(phases[phase].TryGetPipeline(out var pipeline), Is.True);
                return recording.GetSample(SFEcsSystemsProfiler.GetSystems(pipeline)[0], postRun: false).Count;
            }

            Assert.That(Count("FixedUpdate"), Is.EqualTo(1));
            Assert.That(Count("Update"), Is.EqualTo(2));
            Assert.That(Count("LateUpdate"), Is.Zero);

            _service.Dispose();
            SFEcsSystemsProfiler.GetLivePipelines(handles);
            Assert.That(handles.Any(handle => handle.WorldName == worldId), Is.False);
            Assert.That(phases.Values.Any(handle => handle.TryGetPipeline(out _)), Is.False);
        }
    }
}
