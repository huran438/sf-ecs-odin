using UnityEngine;

namespace SFramework.ECS.Runtime
{
    public interface ISFDrawGizmosSelected<T> where T : struct
    {
        void DrawGizmosSelected(Transform transform);
    }
}