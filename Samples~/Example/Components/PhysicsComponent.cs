using Unity.Mathematics;
using VodeoECS;

public struct PhysicsComponent : IDataComponent
{
    public float3 velocity;
    public float deathTime;
}