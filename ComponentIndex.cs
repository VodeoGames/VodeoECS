using System;
using System.Collections.Generic;

namespace VodeoECS
{
    /// <summary>
    /// Index for accessing a specific Component in Component Pools. Components within an Archetype share the same ComponentIndex if their Entities implement the Archetype! 
    /// Component Indices should not be stored permanently as they are invalidated by Component removal, and Archetype or Filter changes. Accessing a Component through a Component Index is more efficient than through an Entity.
    /// </summary>
    public readonly struct ComponentIndex : IEquatable<ComponentIndex>
    {
        /// <summary>
        /// The Null Component Index.
        /// </summary>
        public static ComponentIndex Null { get { return new ComponentIndex( 0 ); } }
        /// <summary>
        /// The integer indexing the specific Component entry within its Taxon. (20 bits max).
        /// </summary>
        public int entry { get { return ( int )( value & ( ( 1 << 20 ) - 1 ) ); } } // 20 bits for entry
        /// <summary>
        /// The integer indexing the Taxon the Component is in. (12 bits max).
        /// </summary>
        public int taxonID { get { return ( int )( value >> 20 ); } } // 12 bits for taxon

        private readonly uint value;

        /// <summary>
        /// For internal use by Component Pools. Constructor for a Component Index.
        /// </summary>
        /// <param name="entry">The integer indexing the specific Component entry within its Taxon. (20 bits max).</param>
        /// <param name="taxonID">The integer indexing the Taxon the Component is in. (12 bits max).</param>
        public ComponentIndex ( int entry, int taxonID )
        {
            this.value = ( ( uint )taxonID << 20 ) | ( uint )entry;
        }
        public bool Equals ( ComponentIndex other ) { return ( this.value == other.value ); }
        public override int GetHashCode ( ) { return ( value ).GetHashCode( ); }
        public static bool operator == ( ComponentIndex x, ComponentIndex y ) { return x.Equals( y ); }
        public static bool operator != ( ComponentIndex x, ComponentIndex y ) { return !( x.Equals( y ) ); }
        public override bool Equals ( object o )
        {
            if ( o == null ) return false;
            else return this == ( ComponentIndex )o;
        }
        private ComponentIndex ( uint value ) { this.value = value; }
    }
}