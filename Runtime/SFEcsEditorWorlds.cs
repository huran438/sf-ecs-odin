#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace SFramework.ECS.Runtime
{
    /// <summary>Discovery of live ECS worlds for editor tools.</summary>
    public static class SFEcsEditorWorlds
    {
        private static readonly List<Handle> Worlds = new();
        private static int _nextId;

        public sealed class Handle
        {
            private readonly WeakReference<SFEcsWorld> _world;

            internal Handle(SFEcsWorld world) => _world = new WeakReference<SFEcsWorld>(world);

            public bool TryGetWorld(out SFEcsWorld world) => _world.TryGetTarget(out world) && world.IsAlive();
        }

        internal static void Track(SFEcsWorld world)
        {
            lock (Worlds)
            {
                world.DebugName = $"World {++_nextId}";
                if (_nextId % 64 == 0) Prune();
                Worlds.Add(new Handle(world));
            }
        }

        internal static void PruneDestroyedWorlds()
        {
            lock (Worlds) Prune();
        }

        public static void GetLiveWorlds(List<Handle> result)
        {
            lock (Worlds)
            {
                Prune();
                result.Clear();
                result.AddRange(Worlds);
            }
        }

        private static void Prune() => Worlds.RemoveAll(handle => !handle.TryGetWorld(out _));
    }
}
#endif
