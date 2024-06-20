using Leopotam.EcsLite;
using SFramework.Core.Runtime;
using UnityEngine.Scripting;



namespace SFramework.ECS.Runtime
{
    public interface ISFWorldsService : ISFService
    {
        public EcsWorld GetWorld(string name = "");
        public EcsWorld[] Worlds { get; }
    }
}