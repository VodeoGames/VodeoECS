using System;
using System.Collections;
using System.Collections.Generic;

namespace VodeoECS
{
    /// <summary>
    /// Queries are made by calling the appropriate Methods on the World class. 
    /// They represent a set of Entities and Components matching a specific Archetype and set of Filters.
    /// Queries are divided into multiple Taxa, each Taxon representing a specific set of Archetypes and set of Filters matching the Query.
    /// </summary>
    public readonly struct Query : IEnumerable<Taxon>
    {
        /// <summary>
        /// The Null Query.
        /// </summary>
        public static Query Null { get { return new Query( new List<Taxon>( ) ); } }
        /// <summary>
        /// The number of Taxa matching the Query.
        /// </summary>
        public int Count { get { return this.taxa.Count; } }
        /// <summary>
        /// Get a Taxon in the Query.
        /// A Taxon is a category of Entities and Components matching a specific set of Archetypes and set of Filters. 
        /// They are mainly used for accessing Component Pools in Burst-compiled code.
        /// </summary>
        /// <param name="i">The index of the Taxon within the Query.</param>
        /// <returns>The Taxon requested. This doesn't contain any data, it's just an index.</returns>
        public Taxon this[int i] { get { return this.taxa[i]; } }
        private readonly List<Taxon> taxa;

        /// <summary>
        /// For internal use by the World class. Queries are normally made by calling the appropriate Methods on the World class.
        /// </summary>
        /// <param name="taxa">The taxa to be included in the Query.</param>
        public Query ( List<Taxon> taxa ) { this.taxa = taxa; }

        /// <summary>
        /// Enumerate through all the Taxa in a given Query.
        /// </summary>
        /// <returns>A Taxon Enumerator.</returns>
        public IEnumerator<Taxon> GetEnumerator ( )
        {
            return ( ( IEnumerable<Taxon> )taxa ).GetEnumerator( );
        }
        IEnumerator IEnumerable.GetEnumerator ( )
        {
            return ( ( IEnumerable )taxa ).GetEnumerator( );
        }
    }
}