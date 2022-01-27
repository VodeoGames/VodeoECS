using Unity.Mathematics;

namespace VodeoECS.Standard
{
    /// <summary>
    /// The spatial rotation of an Entity.
    /// </summary>
    public struct RotationComponent : IDataComponent
    {
        public float4 quaternion;
    }
}