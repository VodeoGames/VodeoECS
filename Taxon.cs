using System;
using System.Collections;
using System.Collections.Generic;

namespace VodeoECS
{
    /// <summary>
    /// A Taxon is a category of Entities and Components matching a specific set of Archetypes and set of Filters. 
    /// They are mainly used for accessing Component Pools in Burst-compiled code.
    /// This doesn't contain any data, it's just an index.
    /// </summary>
    public readonly struct Taxon : IEquatable<Taxon>
    {
        /// <summary>
        /// The Default Taxon (containing Entities which match no Archetypes).
        /// </summary>
        public static Taxon Default { get { return new Taxon( 2 ); } }
        /// <summary>
        /// The Prototype Taxon (containing all Prototypes).
        /// </summary>
        public static Taxon Prototype { get { return new Taxon( 1 ); } }
        /// <summary>
        /// The Null Taxon.
        /// </summary>
        public static Taxon Null { get { return new Taxon( 0 ); } }
        /// <summary>
        /// The unique index of the Taxon.
        /// </summary>
        public readonly ushort ID { get; }
        /// <summary>
        /// For Internal use by the World class. Taxa are normally used by making Queries with the World class.
        /// </summary>
        /// <param name="ID">The unique index of the Taxon.</param>
        public Taxon ( ushort ID ) { this.ID = ID; }
        public bool Equals ( Taxon other ) { return this.ID == other.ID; }
        public override int GetHashCode ( ) { return ( ID ).GetHashCode( ); }
        public static bool operator == ( Taxon x, Taxon y ) { return x.Equals( y ); }
        public static bool operator != ( Taxon x, Taxon y ) { return !( x.Equals( y ) ); }
        public override bool Equals ( object o )
        {
            if ( o == null ) return false;
            else return this == ( Taxon )o;
        }
    }
}