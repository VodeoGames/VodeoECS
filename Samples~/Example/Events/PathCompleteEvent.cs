using VodeoECS;

public struct PathCompleteEvent : IEventECS
{
    public Entity entity;
    public float time;
}