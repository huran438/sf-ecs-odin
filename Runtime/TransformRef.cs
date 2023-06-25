using UnityEngine;

namespace SFramework.ECS.Runtime
{
    public struct TransformRef : ISFComponent
    {
        public Transform value;
        public Vector3 position3 => value.position;
        public Vector2 position2 => value.position;
        public Quaternion rotation => value.rotation;
    }
}