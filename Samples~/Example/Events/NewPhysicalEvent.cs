using Unity.Mathematics;
using VodeoECS;

public struct NewPhysicalEvent :IEventECS
{
    public Entity entity;
    public float4 moment;
    public float3 velocity;
    public float deathTime;
}