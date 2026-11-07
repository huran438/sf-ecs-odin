using SFramework.Core.Runtime;
using UnityEngine.Scripting;



namespace SFramework.ECS.Runtime
{
    public interface ISFWorldsService : ISFService
    {
        public SFEcsWorld GetWorld(string name = "");
        public SFEcsWorld[] Worlds { get; }
    }
}
