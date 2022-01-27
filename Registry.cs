using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;

namespace VodeoECS
{
    /// <summary>
    /// A registry class associating a set of unique items with a RegistryIndex for each one.
    /// </summary>
    /// <typeparam name="T">The type registered in this Registry.</typeparam>
    public class Registry<T> : IEnumerable<T>
    {
        protected readonly List<T> registered;
        protected readonly Dictionary<T, RegistryIndex<T>> indexLookup;

        /// <summary>
        /// The number of unique items registered in this Registry.
        /// </summary>
        public int Count { get { return registered.Count; } }

        /// <summary>
        /// Registry Constructor.
        /// </summary>
        public Registry ( )
        {
            this.registered = new List<T>( );
            this.indexLookup = new Dictionary<T, RegistryIndex<T>>( );
        }

        /// <summary>
        /// Register the given item into the Registry.
        /// </summary>
        /// <param name="item">The item to register.</param>
        /// <returns>The index of the registered item.</returns>
        public RegistryIndex<T> Register ( T item )
        {
            RegistryIndex<T> index = new RegistryIndex<T>( ( ushort )this.Count );
            if ( indexLookup.ContainsKey( item ) ) throw new ArgumentException( item.GetType( ).ToString( ) + " is already registered" );

            this.registered.Add( item );
            this.indexLookup.Add( item, index );

            return index;
        }

        /// <summary>
        /// Read the item with the given index.
        /// </summary>
        /// <param name="index">The index of the item to read.</param>
        /// <returns>The requested item.</returns>
        public T this[RegistryIndex<T> index]
        {
            get => registered[index.ID];
        }

        /// <summary>
        /// Get the RegistryIndex of a registered item.
        /// </summary>
        /// <param name="registered">The registered item.</param>
        /// <returns>The index associated with the registered item.</returns>
        public RegistryIndex<T> GetIndex ( T registered )
        {
            RegistryIndex<T> index;
            if ( this.indexLookup.TryGetValue( registered, out index ) )
            {
                return index;
            }
            else
            {
                throw new FormatException( "No such " + typeof( T ).Name + " registered" );
            }

        }

        /// <summary>
        /// Get the RegistryIndex of a registered item if present, otherwise return false.
        /// </summary>
        /// <param name="registered">The registered item.</param>
        /// <param name="index">Variable to output the index associated with the registered item to.</param>
        /// <returns>Returns true if the item was present, false if not.</returns>
        public bool TryGetIndex ( T registered, out RegistryIndex<T> index )
        {
            return this.indexLookup.TryGetValue( registered, out index );
        }

        /// <summary>
        /// Enumerates through all the items registered in this registry.
        /// </summary>
        /// <returns>Each individual item registered into the registry.</returns>
        public IEnumerator<T> GetEnumerator ( )
        {
            foreach ( T i in this.registered )
            {
                yield return i;
            }
        }

        /// <summary>
        /// Enumerates through the indices of all items registered in this registry.
        /// </summary>
        public IEnumerable<RegistryIndex<T>> ByIndex
        {
            get
            {
                for ( ushort i = 0; i < this.registered.Count; i++ )
                {
                    yield return new RegistryIndex<T>( i );
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator ( )
        {
            return this.GetEnumerator( );
        }
    }
}