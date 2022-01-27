using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VodeoECS
{
    /// <summary>
    /// Index into a Registry.
    /// </summary>
    /// <typeparam name="T">The type associated with the registry that this index is an index into.</typeparam>
    [Serializable]
    public readonly struct RegistryIndex<T> : IEquatable<RegistryIndex<T>>
    {
        private readonly ushort value;
        /// <summary>
        /// Constructs a new RegistryIndex with the given numerical index. For internal use by Registry classes.
        /// </summary>
        /// <param name="ID">The numerical index.</param>
        public RegistryIndex ( ushort ID )
        {
            this.value = ID;
        }
        /// <summary>
        /// The numerical index of the RegistryIndex.
        /// </summary>
        public ushort ID { get { return this.value; } }
        public bool Equals ( RegistryIndex<T> other ) { return this.value == other.value; }
        public override int GetHashCode ( ) { return (typeof( T ), value).GetHashCode( ); }
        public static bool operator == ( RegistryIndex<T> x, RegistryIndex<T> y ) { return x.Equals( y ); }
        public static bool operator != ( RegistryIndex<T> x, RegistryIndex<T> y ) { return !( x.Equals( y ) ); }
        public override bool Equals ( object o )
        {
            if ( o == null ) return false;
            else return this == ( RegistryIndex<T> )o;
        }
    }
}