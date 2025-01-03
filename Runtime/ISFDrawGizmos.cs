using UnityEngine;

namespace SFramework.ECS.Runtime
{
    public interface ISFDrawGizmos<T> where T : struct
    {
        void DrawGizmos(Transform transform);
    }
}