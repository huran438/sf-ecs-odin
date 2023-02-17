using System;
using SFramework.Repositories.Runtime;

namespace SFramework.ECS.Runtime
{
    [Serializable]
    public class SFWorldNode : SFNode
    {
        public SFWorldConfig Config;
        public SFSystemContainer[] FixedUpdateSystems;
        public SFSystemContainer[] UpdateSystems;
        public SFSystemContainer[] LateUpdateSystems;
        public override ISFNode[] Nodes => null;
    }
}