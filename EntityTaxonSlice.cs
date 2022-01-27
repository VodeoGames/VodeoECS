using System.Collections;
using System.Collections.Generic;
using Unity.Collections;

namespace VodeoECS
{
    /// <summary>
    /// A slice into the Entities associated with a single Taxon from a Component Pool. The order of Entities associated with each Component is preserved across all Taxa from the same Query.
    /// Can be passed to Burst Compiled jobs. 
    /// </summary>
    public readonly struct EntityTaxonSlice : IEnumerable<Entity>
    {
        /// <summary>
        /// The Entity at a specific position in the Taxon Slice.
        /// The order of Entities in a Taxon Slice is preserved across all Component Pools sharing the Taxon, and correspond to the order of Components in other types of Taxon Slice.
        /// </summary>
        /// <param name="i">The position of the Entity in the Taxon Slice.</param>
        /// <returns>The Entity at the given position.</returns>
        public Entity this[int i] { get { return slice[i]; } }
        /// <summary>
        /// Number of Data Components in this Slice.
        /// </summary>
        public int Length { get { return slice.Length; } }
        private readonly NativeSlice<Entity> slice;
        /// <summary>
        /// For internal use by the Component Pool classes. Constructs an Entity Taxon Slice.
        /// </summary>
        /// <param name="slice">A NativeSlice into the Entities for a single Taxon.</param>
        public EntityTaxonSlice ( NativeSlice<Entity> slice )
        {
            this.slice = slice;
        }
        /// <summary>
        /// Enumerates through all Data Component values in this Taxon Slice.
        /// The order of Entities in a Taxon Slice is preserved across all Component Pools sharing the Taxon, and correspond to the order of Components in other types of Taxon Slice.
        /// </summary>
        /// <returns>Each Entity in the Taxon Slice.</returns>
        public IEnumerator<Entity> GetEnumerator ( )
        {
            for ( int i = 0; i < this.Length; i++ )
                yield return slice[i];
        }
        IEnumerator IEnumerable.GetEnumerator ( )
        {
            return GetEnumerator( );
        }
    }
}