using Leopotam.EcsLite;

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
        
        public static IEcsSystems Add(this IEcsSystems ecsSystems, params IEcsSystem[] systems)
        {
            foreach (var ecsSystem in systems)
            {
                ecsSystems.Add(ecsSystem);
            }

            return ecsSystems;
        }
    }
}