using System;
using VodeoECS.Internal;

namespace VodeoECS
{
    /// <summary>
    /// Identifier for an Archetype. This doesn't contain any data, it's just a unique identifier.
    /// Archetypes represent sets of Component Types and are matched by Entities which contain the same components. 
    /// They are used with the World class to make queries matching the Archetype.
    /// </summary>
    public readonly struct Archetype : IEquatable<Archetype>
    {
        /// <summary>
        /// The unique Registry Index of the Archetype.
        /// </summary>
        public RegistryIndex<ArchetypeData> index { get; }
        /// <summary>
        /// For Internal use by the World class. Use the appropriate World class methods to register new Archetypes.
        /// </summary>
        /// <param name="index">Unique Registry Index.</param>
        public Archetype ( RegistryIndex<ArchetypeData> index )
        {
            this.index = index;
        }
        public bool Equals ( Archetype other )
        {
            return this.index.Equals( other.index );
        }
        public static bool operator == ( Archetype x, Archetype y ) { return x.Equals( y ); }
        public static bool operator != ( Archetype x, Archetype y ) { return !( x.Equals( y ) ); }
        public override bool Equals ( object o )
        {
            if ( o == null ) return false;
            else return this == ( Archetype )o;
        }
        public override int GetHashCode ( )
        {
            return ( index ).GetHashCode( );
        }
    }
}