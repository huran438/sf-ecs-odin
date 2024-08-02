using System.Linq;
using SFramework.Configs.Runtime;
using SFramework.Core.Runtime;

namespace SFramework.ECS.Runtime
{
    public class SFWorldsConfig : SFNodesConfig
    {
        public override ISFConfigNode[] Children => Worlds;

        public SFWorldNode[] Worlds;
    }
}