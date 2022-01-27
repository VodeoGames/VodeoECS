namespace VodeoECS
{
    /// <summary>
    /// Event emitted to destroy an Entity. Listen to this event to perform system-specific cleanup.
    /// </summary>
    public struct DestroyEntityEvent : IEventECS
    {
        public Entity entity;
    }
}