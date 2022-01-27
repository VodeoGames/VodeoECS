namespace VodeoECS
{
    /// <summary>
    /// Generic event for signaling the destruction of a component of type T.
    /// </summary>
    /// <typeparam name="T">The type of the component that was destroyed.</typeparam>
    public struct ComponentDestructionEvent<T> : IEventECS
    {
        public Entity entity;
        public T component;
    }
}