using System;
using SFramework.Configs.Runtime;

namespace SFramework.ECS.Runtime
{
    [Serializable]
    public class SFWorldNode : SFConfigNode
    {
        public SFWorldConfig Config;
        public SFSystemContainer[] FixedUpdateSystems;
        public SFSystemContainer[] UpdateSystems;
        public SFSystemContainer[] LateUpdateSystems;
        public override ISFConfigNode[] Children => null;
    }
}