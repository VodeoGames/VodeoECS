using VodeoECS;

public struct InitializeFlyerEvent : IEventECS
{
    public Entity flyer;
    public float time;
}