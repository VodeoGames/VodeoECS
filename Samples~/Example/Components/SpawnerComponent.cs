using Unity.Mathematics;
using VodeoECS;

public struct SpawnerComponent : IDataComponent
{
    public float outputRate;
    public float outputVelocity;
    public float lifeTime;
    public float3 position;
    public Entity prototype;
}