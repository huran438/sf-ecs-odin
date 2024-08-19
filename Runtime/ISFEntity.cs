using Leopotam.EcsLite;

namespace SFramework.ECS.Runtime
{
    public interface ISFEntity
    {
        ref EcsPackedEntityWithWorld EcsPackedEntity { get; }
    }
}