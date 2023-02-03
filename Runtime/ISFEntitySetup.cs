using Leopotam.EcsLite;

namespace SFramework.ECS.Runtime
{
    public interface ISFEntitySetup
    {
        void Setup(ref EcsPackedEntityWithWorld packedEntity);
    }
}