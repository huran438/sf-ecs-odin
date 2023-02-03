using System;
using Leopotam.EcsLite;
using Sirenix.OdinInspector;
using UnityEngine;

namespace SFramework.ECS.Runtime
{
    [Serializable]
    public class SFSystemContainer
    {
        public bool Enabled => _enabled;

        public IEcsSystem System => _system;

  
        [HideLabel]
        [HorizontalGroup]
        [SerializeField]
        private bool _enabled = true;
        
        [HideLabel]
        [HorizontalGroup]
        [SerializeReference]
        public IEcsSystem _system;
    }
}