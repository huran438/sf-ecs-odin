using System.Linq;
using SFramework.Core.Runtime;
using SFramework.Repositories.Runtime;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    public class SFWorldsRepository : SFRepository, ISFRepositoryGenerator
    {
        public override ISFNode[] Nodes => Worlds;

        public SFWorldNode[] Worlds;

        public void GetGenerationData(out SFGenerationData[] generationData)
        {
            generationData = new[]
            {
                new SFGenerationData
                {
                    FileName = "SFWorlds",
                    Properties = Worlds.Select(o => $"{_Name}/{o._Name}").ToHashSet()
                }
            };
        }
    }
}