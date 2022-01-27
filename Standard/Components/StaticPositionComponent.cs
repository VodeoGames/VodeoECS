using Unity.Mathematics;

namespace VodeoECS.Standard
{
    /// <summary>
    /// The static position of the Entity.
    /// </summary>
    public struct StaticPositionComponent : IDataComponent
    {
        public float3 position;
    }
}