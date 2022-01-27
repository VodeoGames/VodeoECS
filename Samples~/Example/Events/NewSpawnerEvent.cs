using Unity.Mathematics;
using VodeoECS;

public struct NewSpawnerEvent : IEventECS
{
    public SpawnerComponent spawner;
    public float4 moment;
}