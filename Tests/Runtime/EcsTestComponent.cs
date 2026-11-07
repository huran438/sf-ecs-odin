using SFramework.ECS.Runtime;

namespace SFramework.ECS.Tests
{
    public struct EcsTestPayload : ISFEcsAutoInit<EcsTestPayload>
    {
        public int Value;
        public int Initializations;

        public void AutoInit(ref EcsTestPayload component)
        {
            component.Value += 7;
            component.Initializations++;
        }
    }

    public sealed class EcsTestComponent : SFComponent<EcsTestPayload>
    {
        public void SetSerializedValue(int value)
        {
            _value = new EcsTestPayload { Value = value };
        }
    }
}
