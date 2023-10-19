using Leopotam.EcsLite;
using SFramework.Core.Runtime;

namespace SFramework.ECS.Runtime
{
    public interface ISFWorldsService : ISFService
    {
        public EcsWorld GetWorld(string name = "");
        public EcsWorld[] Worlds { get; }
    }
}