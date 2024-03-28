using System.Linq;
using SFramework.Configs.Runtime;
using SFramework.Core.Runtime;

namespace SFramework.ECS.Runtime
{
    public class SFWorldsConfig : SFConfig, ISFConfigsGenerator
    {
        public override ISFConfigNode[] Children => Worlds;

        public SFWorldNode[] Worlds;

        public void GetGenerationData(out SFGenerationData[] generationData)
        {
            generationData = new[]
            {
                new SFGenerationData
                {
                    FileName = "SFWorlds",
                    Properties = Worlds.Select(o => $"{Id}/{o.Id}").ToHashSet()
                }
            };
        }
    }
}