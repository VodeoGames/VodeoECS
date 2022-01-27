namespace VodeoECS
{
    /// <summary>
    /// Interface for Filter Component Pools.
    /// </summary>
    public interface IFilterComponentPool : IComponentPool
    {
        /// <summary>
        /// Reads a Filter Component from this pool, as an IFilterComponent.
        /// </summary>
        /// <param name="entity">The Entity associated with the Filter Component in this pool.</param>
        /// <returns>The Filter Component as an IFilterComponent.</returns>
        public IFilterComponent ReadBoxedFilterComponent ( Entity entity );
    }
}