using System;
using Leopotam.EcsLite;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace SFramework.ECS.Runtime
{
    [Serializable]
    public class SFSystemContainer
    {
        [HideLabel]
        [HorizontalGroup]
        public bool Enabled = true;
        
        [HideLabel]
        [HorizontalGroup]
        public string System;
    }
}