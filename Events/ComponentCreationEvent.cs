namespace VodeoECS
{
    /// <summary>
    /// Generic event for signaling the creation of a component of type T.
    /// </summary>
    /// <typeparam name="T">The type of the component that was created.</typeparam>
    public struct ComponentCreationEvent<T> : IEventECS
    {
        public Entity entity;
    }
}