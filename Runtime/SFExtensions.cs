using Leopotam.EcsLite;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    public static partial class SFExtensions
    {
        public static ref T Replace<T>(this EcsPool<T> pool, int entity) where T : struct
        {
            if (pool.Has(entity))
            {
                return ref pool.Get(entity);
            }

            return ref pool.Add(entity);
        }
        
        public static IEcsSystems Add(this IEcsSystems ecsSystems, params ISFSystem[] systems)
        {
            foreach (var system in systems)
            {
                if (system is IEcsSystem ecsSystem)
                {
                    ecsSystems.Add(ecsSystem);
                }
                else if (system is ISFSystemsGroup systemsGroup)
                {
                    foreach (var systemG in systemsGroup.Systems)
                    {
                        ecsSystems.Add(systemG);
                    }
                }
            }

            return ecsSystems;
        }
        
        public static bool TryGetEntity(this GameObject gameObject, out int entity)
        {
            return SFEntityMapping.GetEntity(gameObject, out entity);
        }
        
        public static bool TryGetEntityPacked(this GameObject gameObject, out EcsPackedEntityWithWorld packedEntity)
        {
            return SFEntityMapping.GetEntityPacked(gameObject, out packedEntity);
        }
    }
}