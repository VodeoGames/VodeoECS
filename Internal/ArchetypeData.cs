using System;
using System.Collections;
using System.Collections.Generic;

namespace VodeoECS.Internal
{
    /// <summary>
    /// For Internal use by the World class. Implementation Data for Archetypes. 
    /// </summary>
    public readonly struct ArchetypeData : IEquatable<ArchetypeData>
    {
        public static ArchetypeData Null { get { return new ArchetypeData( ); } }

        private static IEqualityComparer<HashSet<RegistryIndex<Type>>> hashComparer = HashSet<RegistryIndex<Type>>.CreateSetComparer( );

        public readonly HashSet<RegistryIndex<Type>> components;
        public readonly HashSet<RegistryIndex<Type>> filters;
        public ArchetypeData ( IEnumerable<RegistryIndex<Type>> components, IEnumerable<RegistryIndex<Type>> filters )
        {
            this.components = new HashSet<RegistryIndex<Type>>( components );
            this.filters = new HashSet<RegistryIndex<Type>>( filters );
        }

        public bool Equals ( ArchetypeData other )
        {
            return this.components.SetEquals( other.components ) && this.filters.SetEquals( other.filters );
        }
        public static bool operator == ( ArchetypeData x, ArchetypeData y ) { return x.Equals( y ); }
        public static bool operator != ( ArchetypeData x, ArchetypeData y ) { return !( x.Equals( y ) ); }
        public override bool Equals ( object o )
        {
            if ( o == null ) return false;
            else return this == ( ArchetypeData )o;
        }
        public override int GetHashCode ( )
        {
            return hashComparer.GetHashCode( components ) + hashComparer.GetHashCode( filters );
        }
    }
}