#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SFramework.ECS.Runtime
{
    /// <summary>Editor-only timing and discovery of ECS system pipelines.</summary>
    public static class SFEcsSystemsProfiler
    {
        private static readonly List<PipelineHandle> Pipelines = new();
        private static volatile Recording _recording;
        [ThreadStatic] private static ISFEcsSystems _currentPipeline;
        [ThreadStatic] private static SystemSlot _currentSlot;

        public sealed class PipelineHandle
        {
            private readonly WeakReference<SFEcsSystems> _pipeline;

            internal PipelineHandle(SFEcsSystems pipeline, string worldName, string phase)
            {
                _pipeline = new WeakReference<SFEcsSystems>(pipeline);
                WorldName = worldName;
                Phase = phase;
            }

            public string WorldName { get; }
            public string Phase { get; }

            public bool TryGetPipeline(out SFEcsSystems pipeline)
            {
                if (_pipeline.TryGetTarget(out pipeline) && pipeline.IsProfilerWorldAlive) return true;
                pipeline = null;
                return false;
            }

            internal void Invalidate() => _pipeline.SetTarget(null);
        }

        public sealed class SystemSlot
        {
            internal readonly ISFEcsSystem System;
            internal readonly SystemSlots ChildSlots;

            internal SystemSlot(ISFEcsSystem system)
            {
                System = system;
                HasRun = system is ISFEcsRunSystem;
                HasPostRun = system is ISFEcsPostRunSystem;
                if (system is SFEcsGroupSystem group)
                {
                    Name = group.Name;
                    ChildSlots = new SystemSlots();
                    foreach (var child in group.NestedSystems) ChildSlots.Add(child);
                }
                else Name = system?.GetType().Name ?? "Missing system";
            }

            public string Name { get; }
            public bool HasRun { get; }
            public bool HasPostRun { get; }
            public bool? GroupEnabled => (System as SFEcsGroupSystem)?.Enabled;
            public IReadOnlyList<SystemSlot> Children => ChildSlots?.All ?? Array.Empty<SystemSlot>();
        }

        public readonly struct Sample
        {
            private readonly double _totalMilliseconds;

            public long Count { get; }
            public double LastMilliseconds { get; }
            public double AverageMilliseconds => Count == 0 ? 0 : _totalMilliseconds / Count;
            public double PeakMilliseconds { get; }

            private Sample(long count, double last, double total, double peak)
            {
                Count = count;
                LastMilliseconds = last;
                _totalMilliseconds = total;
                PeakMilliseconds = peak;
            }

            internal Sample Add(double milliseconds) => new(Count + 1, milliseconds,
                _totalMilliseconds + milliseconds, Math.Max(PeakMilliseconds, milliseconds));
        }

        public sealed class Recording : IDisposable
        {
            internal ConditionalWeakTable<SystemSlot, CallbackSamples> Samples = new();

            public void Resume() => _recording = this;

            public void Pause()
            {
                if (ReferenceEquals(_recording, this)) _recording = null;
            }

            public void Clear() => Samples = new();

            public Sample GetSample(SystemSlot slot, bool postRun)
            {
                if (!Samples.TryGetValue(slot, out var samples)) return default;
                lock (samples) return postRun ? samples.PostRun : samples.Run;
            }

            public void Dispose() => Pause();
        }

        internal sealed class CallbackSamples
        {
            internal Sample Run;
            internal Sample PostRun;
        }

        internal sealed class SystemSlots
        {
            private readonly List<SystemSlot> _all = new();
            internal readonly List<SystemSlot> Run = new();
            internal readonly List<SystemSlot> PostRun = new();
            internal IReadOnlyList<SystemSlot> All { get; }

            internal SystemSlots() => All = _all.AsReadOnly();

            internal void Add(ISFEcsSystem system)
            {
                var slot = new SystemSlot(system);
                _all.Add(slot);
                if (slot.HasRun) Run.Add(slot);
                if (slot.HasPostRun) PostRun.Add(slot);
            }

            internal void Clear()
            {
                _all.Clear();
                Run.Clear();
                PostRun.Clear();
            }
        }

        public static IReadOnlyList<SystemSlot> GetSystems(SFEcsSystems pipeline) => pipeline.ProfilerSlots.All;

        public static void GetLivePipelines(List<PipelineHandle> result)
        {
            lock (Pipelines)
            {
                Pipelines.RemoveAll(handle => !handle.TryGetPipeline(out _));
                result.Clear();
                result.AddRange(Pipelines);
            }
        }

        internal static void Track(SFEcsSystems pipeline, string worldName, string phase)
        {
            lock (Pipelines)
            {
                Pipelines.RemoveAll(handle => !handle.TryGetPipeline(out _));
                if (Pipelines.Exists(handle => handle.TryGetPipeline(out var live) && ReferenceEquals(live, pipeline))) return;
                Pipelines.Add(new PipelineHandle(pipeline, worldName, phase));
            }
        }

        internal static void Untrack(SFEcsSystems pipeline)
        {
            lock (Pipelines)
            {
                for (var i = Pipelines.Count - 1; i >= 0; i--)
                {
                    var handle = Pipelines[i];
                    if (handle.TryGetPipeline(out var live) && !ReferenceEquals(live, pipeline)) continue;
                    handle.Invalidate();
                    Pipelines.RemoveAt(i);
                }
            }
        }

        internal static SampleScope Measure(SFEcsSystems pipeline, int index, bool postRun)
        {
            var recording = _recording;
            if (recording == null) return default;
            var slots = pipeline.ProfilerSlots;
            return new SampleScope(recording, pipeline, (postRun ? slots.PostRun : slots.Run)[index], postRun);
        }

        internal static SampleScope MeasureChild(ISFEcsSystems pipeline, SFEcsGroupSystem group, int index, bool postRun)
        {
            var recording = _recording;
            if (recording == null || !ReferenceEquals(_currentPipeline, pipeline) ||
                !ReferenceEquals(_currentSlot?.System, group)) return default;
            var slots = _currentSlot.ChildSlots;
            return new SampleScope(recording, pipeline, (postRun ? slots.PostRun : slots.Run)[index], postRun);
        }

        internal readonly struct SampleScope : IDisposable
        {
            private static readonly double MillisecondsPerTick = 1000d / Stopwatch.Frequency;
            private readonly Recording _owner;
            private readonly ConditionalWeakTable<SystemSlot, CallbackSamples> _samples;
            private readonly ISFEcsSystems _parentPipeline;
            private readonly SystemSlot _parentSlot;
            private readonly SystemSlot _slot;
            private readonly bool _postRun;
            private readonly long _startedAt;

            internal SampleScope(Recording owner, ISFEcsSystems pipeline, SystemSlot slot, bool postRun)
            {
                _owner = owner;
                _samples = owner.Samples;
                _parentPipeline = _currentPipeline;
                _parentSlot = _currentSlot;
                _slot = slot;
                _postRun = postRun;
                _currentPipeline = pipeline;
                _currentSlot = slot;
                _startedAt = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                if (_owner == null) return;
                var milliseconds = (Stopwatch.GetTimestamp() - _startedAt) * MillisecondsPerTick;
                _currentPipeline = _parentPipeline;
                _currentSlot = _parentSlot;
                if (!ReferenceEquals(_recording, _owner) || !ReferenceEquals(_owner.Samples, _samples)) return;

                var samples = _samples.GetValue(_slot, static _ => new CallbackSamples());
                lock (samples)
                {
                    if (_postRun) samples.PostRun = samples.PostRun.Add(milliseconds);
                    else samples.Run = samples.Run.Add(milliseconds);
                }
            }
        }
    }
}
#endif
