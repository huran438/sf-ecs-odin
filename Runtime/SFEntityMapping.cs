using System.Collections.Generic;
using SFramework.Core.Runtime;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    public static class SFEntityMapping
    {
        private static readonly Dictionary<int, SFEcsPackedEntityWithWorld> _packedEntities;

        static SFEntityMapping()
        {
            _packedEntities = new Dictionary<int, SFEcsPackedEntityWithWorld>();
        }

        static internal void AddMapping(GameObject gameObject, ref SFEcsWorld world, ref int entity)
        {
            _packedEntities[gameObject.GetInstanceID()] = world.PackEntityWithWorld(entity);
        }

        static internal void AddMapping(GameObject gameObject, ref SFEcsPackedEntityWithWorld entity)
        {
            _packedEntities[gameObject.GetInstanceID()] = entity;
        }

        static internal void RemoveMapping(GameObject gameObject)
        {
            var instanceId = gameObject.GetInstanceID();

            if (_packedEntities.ContainsKey(instanceId))
            {
                _packedEntities.Remove(instanceId);
            }
        }

        internal static void RemoveMappings(SFEcsWorld world)
        {
            var keys = new List<int>();
            foreach (var entry in _packedEntities)
            {
                if (entry.Value.World == world) keys.Add(entry.Key);
            }
            foreach (var key in keys) _packedEntities.Remove(key);
        }

        public static bool GetEntity(GameObject gameObject, out SFEcsWorld world, out int entity)
        {
            if (gameObject == null)
            {
                SFDebug.Log(LogType.Warning, "GameObject is null");
                entity = default;
                world = default;
                return false;
            }

            var instanceId = gameObject.GetInstanceID();

            if (_packedEntities.TryGetValue(instanceId, out var packedEntityWithWorld))
            {
                return packedEntityWithWorld.Unpack(out world, out entity);
            }

            entity = default;
            world = default;
            return false;
        }

        public static bool GetEntity(GameObject gameObject, out int entity)
        {
            if (gameObject == null)
            {
                SFDebug.Log(LogType.Warning, "GameObject is null");
                entity = default;
                return false;
            }

            var instanceId = gameObject.GetInstanceID();

            if (_packedEntities.TryGetValue(instanceId, out var packedEntityWithWorld))
            {
                return packedEntityWithWorld.Unpack(out _, out entity);
            }

            entity = default;
            return false;
        }

        public static bool GetEntityPacked(GameObject gameObject, out SFEcsPackedEntityWithWorld packedEntity)
        {
            if (gameObject == null)
            {
                SFDebug.Log(LogType.Warning, "GameObject is null");
                packedEntity = default;
                return false;
            }

            var instanceId = gameObject.GetInstanceID();

            if (_packedEntities.TryGetValue(instanceId, out var packedEntityWithWorld))
            {
                packedEntity = packedEntityWithWorld;
                return true;
            }

            packedEntity = default;
            return false;
        }

        public static void Clear()
        {
            _packedEntities.Clear();
        }
    }
}
