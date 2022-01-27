using System;

namespace VodeoECS
{
    /// <summary>
    /// Entity struct: this is simply an identifier and doesn't contain any component data.
    /// </summary>
    [Serializable]
    public readonly struct Entity : IEquatable<Entity>
    {
        /// <summary>
        /// The Null Entity identifier.
        /// </summary>
        public static Entity Null { get { return new Entity( 0 ); } }
        /// <summary>
        /// The unique integer identifying this Entity.
        /// </summary>
        public int ID { get { return ( int )( value & ( ( ( uint )1 << 31 ) - 1 ) ); } } // 31 bits for ID
        /// <summary>
        /// Is the Entity a prototype?
        /// </summary>
        public bool prototype { get { return ( value >> 31 ) > 0; } } // 1 prototype bit

        private readonly uint value;

        /// <summary>
        /// Entity Constructor. For internal use only, entities should normally be created from the World class.
        /// </summary>
        /// <param name="ID">The unique integer identifying this Entity.</param>
        /// <param name="prototype">Is the Entity a prototype?</param>
        public Entity ( int ID, bool prototype )
        {
            this.value = ( ( uint )ID ) | ( uint )( prototype ? 1 : 0 ) << 31;
        }
        public bool Equals ( Entity other ) { return this.value == other.value; }
        public override int GetHashCode ( ) { return ( value ).GetHashCode( ); }
        public static bool operator == ( Entity x, Entity y ) { return x.Equals( y ); }
        public static bool operator != ( Entity x, Entity y ) { return !( x.Equals( y ) ); }
        public override bool Equals ( object o )
        {
            if ( o == null ) return false;
            else return this == ( Entity )o;
        }
        private Entity ( uint value ) { this.value = value; }
    }
}