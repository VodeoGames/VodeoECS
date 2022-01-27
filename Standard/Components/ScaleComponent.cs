using Unity.Mathematics;

namespace VodeoECS.Standard
{
    /// <summary>
    /// The scale transform of an Entity.
    /// </summary>
    public struct ScaleComponent : IDataComponent
    {
        public float3 scale;
    }
}