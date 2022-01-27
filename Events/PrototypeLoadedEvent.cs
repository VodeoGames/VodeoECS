namespace VodeoECS
{
    /// <summary>
    /// This event is emitted whenever a new Prototype is loaded.
    /// </summary>
    public struct PrototypeLoadedEvent : IEventECS
    {
        public Entity prototype;
    }
}