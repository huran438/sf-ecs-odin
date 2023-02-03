using System;
using SFramework.Core.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    [Serializable]
    public class SFWorldContainer : SFDatabaseNode
    {
        public SFWorldConfig Config => _config;

        public SFSystemContainer[] FixedUpdateSystems => _fixedUpdateSystems;

        public SFSystemContainer[] UpdateSystems => _updateSystems;

        public SFSystemContainer[] LateUpdateSystems => _lateUpdateSystems;


        [SerializeField]
        private SFWorldConfig _config;
        
        [SerializeField]
        private SFSystemContainer[] _fixedUpdateSystems;
        
        [SerializeField]
        private SFSystemContainer[] _updateSystems;
        
        [SerializeField]
        private SFSystemContainer[] _lateUpdateSystems;
        public override ISFDatabaseNode[] Children => null;
    }
}