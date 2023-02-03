using System.Linq;
using SFramework.Core.Runtime;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    [CreateAssetMenu(fileName = "dtb_worlds", menuName = "SFramework/Database/World")]
    public class SFWorldsDatabase : SFDatabase, ISFDatabaseGenerator
    {
        public override string Title => "Worlds";
        public override ISFDatabaseNode[] Nodes => _worlds;
        
        [SerializeField]
        private SFWorldContainer[] _worlds;

        public void GetGenerationData(out SFGenerationData[] generationData)
        {
            generationData = new[]
            {
                new SFGenerationData
                {
                    FileName = "SFWorlds",
                    Properties = _worlds.Select(o => o.Name).ToHashSet()
                }
            };
        }
    }
}